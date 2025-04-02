// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OTel;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Implements token endpoint operations using IdentityModel
/// </summary>
[Obsolete(Constants.AtmPublicSurfaceInternal, UrlFormat = Constants.AtmPublicSurfaceLink)]
public class ClientCredentialsTokenEndpointService(
    AccessTokenManagementMetrics metrics,
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<ClientCredentialsClient> options,
    IClientAssertionService clientAssertionService,
    IDPoPKeyStore dPoPKeyMaterialService,
    IDPoPProofService dPoPProofService,
    ILogger<ClientCredentialsTokenEndpointService> logger) : IClientCredentialsTokenEndpointService
{
    /// <inheritdoc/>
    public virtual async Task<ClientCredentialsToken> RequestToken(
        string clientName,
        TokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {

        var client = options.Get(clientName);

        if (string.IsNullOrWhiteSpace(client.ClientId))
        {
            throw new InvalidOperationException($"No ClientId configured for client {clientName}");
        }
        if (string.IsNullOrWhiteSpace(client.TokenEndpoint))
        {
            throw new InvalidOperationException($"No TokenEndpoint configured for client {clientName}");
        }

        using var logScope = logger.BeginScope(
            (OTelParameters.ClientId, client.ClientId)
        );

        var request = new ClientCredentialsTokenRequest
        {
            Address = client.TokenEndpoint,
            Scope = client.Scope,
            ClientId = client.ClientId,
            ClientSecret = client.ClientSecret,
            ClientCredentialStyle = client.ClientCredentialStyle,
            AuthorizationHeaderStyle = client.AuthorizationHeaderStyle
        };

        request.Parameters.AddRange(client.Parameters);

        parameters ??= new TokenRequestParameters();

        if (!string.IsNullOrWhiteSpace(parameters.Scope))
        {
            request.Scope = parameters.Scope;
        }

        if (!string.IsNullOrWhiteSpace(parameters.Resource))
        {
            request.Resource.Clear();
            request.Resource.Add(parameters.Resource);
        }
        else if (!string.IsNullOrWhiteSpace(client.Resource))
        {
            request.Resource.Clear();
            request.Resource.Add(client.Resource);
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
            var assertion = await clientAssertionService.GetClientAssertionAsync(clientName).ConfigureAwait(false);

            if (assertion != null)
            {
                request.ClientAssertion = assertion;
                request.ClientCredentialStyle = ClientCredentialStyle.PostBody;
            }
        }

        request.Options.TryAdd(ClientCredentialsTokenManagementDefaults.TokenRequestParametersOptionsName, parameters);

        var key = await dPoPKeyMaterialService.GetKeyAsync(clientName);
        if (key != null)
        {
            logger.CreatingDPoPProofToken();

            var proof = await dPoPProofService.CreateProofTokenAsync(new DPoPProofRequest
            {
                Url = request.Address!,
                Method = "POST",
                DPoPJsonWebKey = key.JsonWebKey,
            });
            request.DPoPProofToken = proof?.ProofToken;
        }

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

        logger.RequestingClientCredentialsAccessToken(request.Address);
        var response = await httpClient.RequestClientCredentialsTokenAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.IsError &&
            (response.Error == OidcConstants.TokenErrors.UseDPoPNonce || response.Error == OidcConstants.TokenErrors.InvalidDPoPProof) &&
            key != null &&
            response.DPoPNonce != null)
        {
            logger.DPoPErrorDuringTokenRefreshWillRetryWithServerNonce(response.Error);

            metrics.DPoPNonceErrorRetry(request.ClientId, AccessTokenManagementMetrics.TokenRequestType.ClientCredentials, response.Error);

            var proof = await dPoPProofService.CreateProofTokenAsync(new DPoPProofRequest
            {
                Url = request.Address!,
                Method = "POST",
                DPoPJsonWebKey = key.JsonWebKey,
                DPoPNonce = response.DPoPNonce
            });
            request.DPoPProofToken = proof?.ProofToken;

            if (request.DPoPProofToken != null)
            {
                response = await httpClient.RequestClientCredentialsTokenAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }

        if (response.IsError)
        {
            metrics.TokenRetrievalFailed(request.ClientId, AccessTokenManagementMetrics.TokenRequestType.ClientCredentials, response.Error);
            logger.FailedToRequestAccessTokenForClient(clientName, response.Error, response.ErrorDescription);

            return new ClientCredentialsToken
            {
                Error = response.Error,
                ClientId = request.ClientId
            };
        }

        metrics.TokenRetrieved(request.ClientId, AccessTokenManagementMetrics.TokenRequestType.ClientCredentials);

        var token = new ClientCredentialsToken
        {
            AccessToken = response.AccessToken,
            AccessTokenType = response.TokenType,
            DPoPJsonWebKey = key?.JsonWebKey,
            Expiration = response.ExpiresIn == 0
                ? DateTimeOffset.MaxValue
                : DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn),
            Scope = response.Scope,
            ClientId = request.ClientId
        };

        logger.ClientCredentialsTokenForClientRetrieved(clientName, token.AccessTokenType, token.Expiration);
        return token;
    }
}
