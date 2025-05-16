// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OTel;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.Internal;

/// <summary>
/// Implements the logic needed to actually fetch an OAuth2.0 Client Credentials token.
/// </summary>
internal class ClientCredentialsTokenClient(
    AccessTokenManagementMetrics metrics,
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<ClientCredentialsClient> options,
    IClientAssertionService clientAssertionService,
    TimeProvider time,
    IDPoPKeyStore dPoPKeyMaterialService,
    IDPoPProofService dPoPProofService,
    ILogger<ClientCredentialsTokenClient> logger) : IClientCredentialsTokenEndpoint
{
    /// <inheritdoc/>
    public virtual async Task<TokenResult<ClientCredentialsToken>> RequestAccessTokenAsync(
        ClientName clientName,
        TokenRequestParameters? parameters = null,
        CT ct = default)
    {

        var client = options.Get(clientName.ToString());

        if (client.ClientId == null)
        {
            throw new InvalidOperationException($"No ClientId configured for client {clientName}");
        }
        if (client.TokenEndpoint == null)
        {
            throw new InvalidOperationException($"No TokenEndpoint configured for client {clientName}");
        }

        if (client.ClientSecret == null)
        {
            throw new InvalidOperationException($"No ClientSecret configured for client {clientName}");
        }

        using var logScope = logger.BeginScope(
            (OTelParameters.ClientId, client.ClientId)
        );

        var request = new ClientCredentialsTokenRequest
        {
            Address = client.TokenEndpoint.ToString(),
            Scope = client.Scope?.ToString(),
            ClientId = client.ClientId.Value.ToString(),
            ClientSecret = client.ClientSecret?.ToString(),
            ClientCredentialStyle = client.ClientCredentialStyle,
            AuthorizationHeaderStyle = client.AuthorizationHeaderStyle
        };

        request.Parameters.AddRange(client.Parameters);

        parameters ??= new TokenRequestParameters();

        if (parameters.Scope != null)
        {
            request.Scope = parameters.Scope.ToString();
        }

        if (parameters.Resource != null)
        {
            request.Resource.Clear();
            request.Resource.Add(parameters.Resource.Value.ToString());
        }
        else if (client.Resource != null)
        {
            request.Resource.Clear();
            request.Resource.Add(client.Resource.Value.ToString());
        }

        request.Parameters.AddRange(parameters.Parameters);

        // if assertion gets passed in explicitly, use it.
        // otherwise call assertion service
        if (parameters.Assertion != null)
        {
            request.ClientAssertion = parameters.Assertion;
            request.ClientCredentialStyle = ClientCredentialStyle.PostBody;
        }
        else
        {
            var assertion = await clientAssertionService.GetClientAssertionAsync(clientName, parameters, ct: ct)
                .ConfigureAwait(false);

            if (assertion != null)
            {
                request.ClientAssertion = assertion;
                request.ClientCredentialStyle = ClientCredentialStyle.PostBody;
            }
        }

        request.Options.TryAdd(
            ClientCredentialsTokenManagementDefaults.TokenRequestParametersOptionsName, parameters);

        var dpopJsonWebKey = await dPoPKeyMaterialService.GetKeyAsync(clientName, ct);

        if (dpopJsonWebKey != null)
        {
            request.DPoPProofToken = await CreateDPoPProofToken(client.TokenEndpoint, dpopJsonWebKey.Value, ct: ct);
        }

        var httpClient = GetHttpClient(client);

        logger.RequestingClientCredentialsAccessToken(LogLevel.Debug, client.TokenEndpoint);
        var response = await httpClient.RequestClientCredentialsTokenAsync(request, ct).ConfigureAwait(false);

        // Retry policy: if we get a DPoP nonce error, retry with the server nonce
        // Note, it's not possible to implement this using Polly, because you can inject
        // a (already configured) http client.
        if (response.IsError // There was an error
            && DPoPErrors.IsDPoPError(response.Error) // for DPOP
            && dpopJsonWebKey != null // And we have the information needed to request a new key
            && response.DPoPNonce != null)
        {
            logger.DPoPErrorDuringTokenRefreshWillRetryWithServerNonce(LogLevel.Debug, response.Error);
            metrics.DPoPNonceErrorRetry(ClientId.Parse(request.ClientId), response.Error);

            request.DPoPProofToken = await CreateDPoPProofToken(
                tokenEndpoint: client.TokenEndpoint,
                dpopJsonWebKeyString: dpopJsonWebKey.Value,
                dPoPNonce: DPoPNonce.Parse(response.DPoPNonce),
                ct: ct);

            if (request.DPoPProofToken != null)
            {
                response = await httpClient.RequestClientCredentialsTokenAsync(request, ct).ConfigureAwait(false);
            }
        }

        if (response.IsError)
        {
            // Turns out token retrieval (even after possible retry) has failed.
            // Return it as a failure.
            metrics.TokenRetrievalFailed(request.ClientId, AccessTokenManagementMetrics.TokenRequestType.ClientCredentials, response.Error);
            logger.FailedToRequestAccessTokenForClient(LogLevel.Error, clientName, response.Error, response.ErrorDescription);

            return TokenResult.Failure(response.Error ?? "Failed to acquire access token", response.ErrorDescription ?? "unknown");
        }

        var token = new ClientCredentialsToken
        {
            AccessToken = AccessTokenString.Parse(response.AccessToken ?? throw new InvalidOperationException("Access token should not be null")),
            AccessTokenType = AccessTokenType.ParseOrDefault(response.TokenType),
            DPoPJsonWebKey = dpopJsonWebKey,
            Expiration = response.ExpiresIn == 0
                ? DateTimeOffset.MaxValue
                : time.GetUtcNow().AddSeconds(response.ExpiresIn),
            Scope = Scope.ParseOrDefault(response.Scope),
            ClientId = ClientId.Parse(request.ClientId)
        };

        metrics.TokenRetrieved(request.ClientId, AccessTokenManagementMetrics.TokenRequestType.ClientCredentials);
        logger.ClientCredentialsTokenForClientRetrieved(LogLevel.Debug, clientName, token.AccessTokenType, token.Expiration);
        return token;
    }

    private async Task<string?> CreateDPoPProofToken(
        Uri tokenEndpoint,
        ProofKeyString dpopJsonWebKeyString,
        DPoPNonce? dPoPNonce = null,
        CT ct = default)
    {
        logger.CreatingDPoPProofToken(LogLevel.Debug);

        var proof = await dPoPProofService.CreateProofTokenAsync(new DPoPProof
        {
            Url = tokenEndpoint,
            Method = HttpMethod.Post,
            ProofKey = dpopJsonWebKeyString,
            DPoPNonce = dPoPNonce
        }, ct);

        return proof?.ToString();
    }

    private HttpClient GetHttpClient(ClientCredentialsClient client)
    {
        HttpClient httpClient;
        if (client.HttpClient != null)
        {
            httpClient = client.HttpClient;
        }
        else if (!string.IsNullOrWhiteSpace(client.HttpClientName))
        {
            httpClient = httpClientFactory.CreateClient(client.HttpClientName);
        }
        else
        {
            httpClient = httpClientFactory.CreateClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName);
        }

        return httpClient;
    }
}
