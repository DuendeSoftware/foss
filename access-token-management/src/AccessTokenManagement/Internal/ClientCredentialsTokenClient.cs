// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Text.Json;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OTel;
using Duende.IdentityModel;
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
    IOptions<ClientCredentialsTokenManagementOptions> managementOptions,
    IClientAssertionService clientAssertionService,
    TimeProvider time,
    IDPoPKeyStore dPoPKeyMaterialService,
    IDPoPProofService dPoPProofService,
    ILogger<ClientCredentialsTokenClient> logger) : IClientCredentialsTokenEndpoint
{
    /// <inheritdoc/>
    public virtual async Task<TokenResult<ClientCredentialsToken>> RequestAccessTokenAsync(
        ClientCredentialsClientName clientName,
        TokenRequestParameters? parameters = null,
        CT ct = default)
    {

        var client = options.Get(clientName.ToString());

        if (client.ClientId == null)
        {
            throw new InvalidOperationException($"No {nameof(client.ClientId)} configured for client {clientName}");
        }
        if (client.TokenEndpoint == null)
        {
            if (client.MetadataAddress == null)
            {
                throw new InvalidOperationException($"No {nameof(client.TokenEndpoint)} or {nameof(client.MetadataAddress)} configured for client {clientName}");
            }

            client.TokenEndpoint = await GetTokenEndpoint(client, ct).ConfigureAwait(false);
        }

        Debug.Assert(client.TokenEndpoint != null);

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

        var httpClient = ResolveHttpClient(client);

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
                dpopProofKey: dpopJsonWebKey.Value,
                dPoPNonce: DPoPNonce.Parse(response.DPoPNonce),
                ct: ct);

            // Regenerate the client assertion to ensure a fresh jti on retry.
            // Only apply when using the service-based assertion path (not the static Assertion parameter).
            if (parameters.Assertion == null)
            {
                var freshAssertion = await clientAssertionService.GetClientAssertionAsync(clientName, parameters, ct: ct)
                    .ConfigureAwait(false);
                if (freshAssertion != null)
                {
                    request.ClientAssertion = freshAssertion;
                    request.ClientCredentialStyle = ClientCredentialStyle.PostBody;
                }
            }

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
            AccessToken = TokenParsing.ParseAccessToken(
                response.AccessToken ?? throw new InvalidOperationException("Access token should not be null"),
                managementOptions.Value.MaxAccessTokenLength,
                nameof(ClientCredentialsTokenManagementOptions) + "." + nameof(ClientCredentialsTokenManagementOptions.MaxAccessTokenLength)),
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
        DPoPProofKey dpopProofKey,
        DPoPNonce? dPoPNonce = null,
        CT ct = default)
    {
        logger.CreatingDPoPProofToken(LogLevel.Debug);

        var proof = await dPoPProofService.CreateProofTokenAsync(new DPoPProofRequest
        {
            Url = tokenEndpoint,
            Method = HttpMethod.Post,
            DPoPProofKey = dpopProofKey,
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

    private HttpClient ResolveHttpClient(ClientCredentialsClient client) => client.HttpClient ?? GetHttpClient(client);

    private async Task<Uri> GetTokenEndpoint(ClientCredentialsClient client, CT ct)
    {
        Debug.Assert(client.MetadataAddress != null);

        var metadataAddress = client.MetadataAddress;

        if (!metadataAddress.IsAbsoluteUri || !DiscoveryEndpoint.IsValidScheme(metadataAddress))
        {
            throw new InvalidOperationException($"Metadata address for client {client.ClientId} must be an absolute HTTP or HTTPS URI");
        }

        var policy = new DiscoveryPolicy
        {
            Authority = metadataAddress.GetLeftPart(UriPartial.Authority)
        };

        if (!DiscoveryEndpoint.IsSecureScheme(metadataAddress, policy))
        {
            throw new InvalidOperationException($"HTTPS is required for metadata address for client {client.ClientId}");
        }

        var httpClient = ResolveHttpClient(client);

        // Fetch directly to preserve the document URL and avoid retrieving signing keys.
        using var response = await httpClient.GetAsync(metadataAddress, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var document = JsonDocument.Parse(content);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Discovery document for client {client.ClientId} must be a JSON object");
        }

        if (!document.RootElement.TryGetProperty(OidcConstants.Discovery.TokenEndpoint, out var tokenEndpointElement) ||
            tokenEndpointElement.ValueKind != JsonValueKind.String ||
            tokenEndpointElement.GetString() is not { Length: > 0 } tokenEndpoint)
        {
            throw new InvalidOperationException($"No token endpoint found in discovery document for client {client.ClientId}");
        }

        if (!Uri.TryCreate(tokenEndpoint, UriKind.Absolute, out var tokenEndpointUri) ||
            !DiscoveryEndpoint.IsValidScheme(tokenEndpointUri))
        {
            throw new InvalidOperationException($"Invalid discovery document for client {client.ClientId}: Malformed endpoint: {tokenEndpoint}");
        }

        if (!DiscoveryEndpoint.IsSecureScheme(tokenEndpointUri, policy))
        {
            throw new InvalidOperationException($"Invalid discovery document for client {client.ClientId}: Endpoint does not use HTTPS: {tokenEndpoint}");
        }

        if (!string.Equals(tokenEndpointUri.Authority, metadataAddress.Authority, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Invalid discovery document for client {client.ClientId}: Endpoint is on a different host than authority: {tokenEndpoint}");
        }

        var endpointValidation = policy.AuthorityValidationStrategy.IsEndpointValid(tokenEndpoint, [policy.Authority]);
        if (!endpointValidation.Success)
        {
            throw new InvalidOperationException($"Invalid discovery document for client {client.ClientId}: {endpointValidation.ErrorMessage}");
        }

        return tokenEndpointUri;
    }
}
