// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OTel;
using Duende.AccessTokenManagement.Types;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.Internal;

/// <summary>
/// Implements the logic needed to actually fetch an OAuth2.0 Client Credentials token. 
/// </summary>
internal class ClientCredentialsTokenEndpointService(
    AccessTokenManagementMetrics metrics,
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<ClientCredentialsClient> options,
    IClientAssertionService clientAssertionService,
    TimeProvider time,
    IDPoPKeyStore dPoPKeyMaterialService,
    IDPoPProofService dPoPProofService,
    ILogger<ClientCredentialsTokenEndpointService> logger) : IClientCredentialsTokenEndpointService
{
    /// <inheritdoc/>
    public virtual async Task<TokenResult<ClientCredentialsToken>> RequestToken(
        ClientCredentialsClientName clientName,
        TokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {

        var client = options.Get(clientName.Value);

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
            ClientId = client.ClientId.ToString(),
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
            request.Resource.Add(parameters.Resource.ToString());
        }
        else if (client.Resource != null)
        {
            request.Resource.Clear();
            request.Resource.Add(client.Resource.ToString());
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
            var assertion = await clientAssertionService.GetClientAssertionAsync(clientName, parameters, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (assertion != null)
            {
                request.ClientAssertion = assertion;
                request.ClientCredentialStyle = ClientCredentialStyle.PostBody;
            }
        }

        request.Options.TryAdd(
            ClientCredentialsTokenManagementDefaults.TokenRequestParametersOptionsName, parameters);

        var dpopJsonWebKey = await dPoPKeyMaterialService.GetKeyAsync(clientName, cancellationToken);

        if (dpopJsonWebKey != null)
        {
            request.DPoPProofToken = await CreateDPoPProofToken(client.TokenEndpoint, dpopJsonWebKey, cancellationToken: cancellationToken);
        }

        var httpClient = GetHttpClient(client);

        logger.RequestingClientCredentialsAccessToken(client.TokenEndpoint);
        var response = await httpClient.RequestClientCredentialsTokenAsync(request, cancellationToken).ConfigureAwait(false);

        // Retry policy: if we get a DPoP nonce error, retry with the server nonce
        if (response.IsError &&
            (response.Error == OidcConstants.TokenErrors.UseDPoPNonce || response.Error == OidcConstants.TokenErrors.InvalidDPoPProof) &&
            dpopJsonWebKey != null &&
            response.DPoPNonce != null)
        {
            logger.DPoPErrorDuringTokenRefreshWillRetryWithServerNonce(response.Error);
            metrics.DPoPNonceErrorRetry(request.ClientId, AccessTokenManagementMetrics.TokenRequestType.ClientCredentials, response.Error);

            request.DPoPProofToken = await CreateDPoPProofToken(
                tokenEndpoint: client.TokenEndpoint,
                dpopJsonWebKey: dpopJsonWebKey,
                dPoPNonce: DPoPNonce.Parse(response.DPoPNonce),
                cancellationToken: cancellationToken);

            if (request.DPoPProofToken != null)
            {
                response = await httpClient.RequestClientCredentialsTokenAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }

        if (response.IsError)
        {
            // Turns out token retrieval (even after possible retry) has failed. 
            // Return it as a failure. 
            metrics.TokenRetrievalFailed(request.ClientId, AccessTokenManagementMetrics.TokenRequestType.ClientCredentials, response.Error);
            logger.FailedToRequestAccessTokenForClient(clientName, response.Error, response.ErrorDescription);

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
        logger.ClientCredentialsTokenForClientRetrieved(clientName, token.AccessTokenType, token.Expiration);
        return token;
    }

    private async Task<string?> CreateDPoPProofToken(
        Uri tokenEndpoint, 
        DPoPJsonWebKey dpopJsonWebKey, 
        DPoPNonce? dPoPNonce = null,
        CancellationToken cancellationToken = default)
    {
        logger.CreatingDPoPProofToken();

        var proof = await dPoPProofService.CreateProofTokenAsync(new DPoPProofRequest
        {
            Url = tokenEndpoint,
            Method = HttpMethod.Post,
            DPoPJsonWebKey = dpopJsonWebKey,
            DPoPNonce = dPoPNonce
        }, cancellationToken);

        return proof?.ProofToken.Value;
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
