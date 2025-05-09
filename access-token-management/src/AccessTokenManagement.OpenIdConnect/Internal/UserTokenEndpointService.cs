// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.Internal;
using Duende.AccessTokenManagement.OTel;
using Duende.AccessTokenManagement.Types;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

/// <summary>
/// Implements token endpoint operations using IdentityModel
/// </summary>
internal class UserTokenEndpointService(
    AccessTokenManagementMetrics metrics,
    IOpenIdConnectConfigurationService configurationService,
    IOptions<UserTokenManagementOptions> options,
    IClientAssertionService clientAssertionService,
    IDPoPProofService dPoPProofService,
    ILogger<UserTokenEndpointService> logger) : IUserTokenEndpointService
{
    /// <inheritdoc/>
    public async Task<TokenResult<UserToken>> RefreshAccessTokenAsync(
        UserRefreshToken refreshToken,
        UserTokenRequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var oidc = await configurationService
            .GetOpenIdConnectConfigurationAsync(parameters.ChallengeScheme)
            .ConfigureAwait(false);

        // Add the ClientID to all subsequent log messages
        using var logScope = logger.BeginScope(
            (OTelParameters.ClientId, oidc.ClientId)
        );

        var tokenEndpoint = oidc.TokenEndpoint ?? throw new InvalidOperationException("TokenEndpoint is null");

        logger.RefreshingAccessTokenUsingRefreshToken(refreshToken.RefreshToken, hashAlgorithm: Crypto.HashData);

        var request = new RefreshTokenRequest
        {
            Address = tokenEndpoint.ToString(),
            ClientId = oidc.ClientId?.Value ?? throw new InvalidOperationException("ClientID is null"),
            ClientSecret = oidc.ClientSecret?.Value ?? throw new InvalidOperationException("ClientSecret is null"),
            ClientCredentialStyle = options.Value.ClientCredentialStyle,
            RefreshToken = refreshToken.RefreshToken.ToString()
        };

        request.Options.TryAdd(ClientCredentialsTokenManagementDefaults.TokenRequestParametersOptionsName, parameters);

        if (parameters.Scope != null)
        {
            request.Scope = parameters.Scope.Value;
        }

        if (parameters.Resource != null)
        {
            request.Resource.Add(parameters.Resource.Value);
        }

        if (parameters.Assertion != null)
        {
            request.ClientAssertion = parameters.Assertion;
            request.ClientCredentialStyle = ClientCredentialStyle.PostBody;
        }
        else
        {
            var assertion = await clientAssertionService
                .GetClientAssertionAsync(
                    clientName: OpenIdConnectTokenManagementDefaults.ClientCredentialsClientNamePrefix + oidc.Scheme,
                    parameters,
                    cancellationToken)
                .ConfigureAwait(false);

            if (assertion != null)
            {
                request.ClientAssertion = assertion;
                request.ClientCredentialStyle = ClientCredentialStyle.PostBody;
            }
        }

        var dPoPJsonWebKey = refreshToken.DPoPJsonWebKey;
        if (dPoPJsonWebKey != null)
        {
            var proof = await dPoPProofService.CreateProofTokenAsync(new DPoPProofRequest
            {
                Url = tokenEndpoint,
                Method = HttpMethod.Post,
                DPoPJsonWebKey = dPoPJsonWebKey,
            }, cancellationToken);

            request.DPoPProofToken = proof?.ProofToken.Value;
        }

        logger.SendingRefreshTokenRequest(tokenEndpoint);
        var response = await oidc.HttpClient!.RequestRefreshTokenAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.IsError &&
            (response.Error == OidcConstants.TokenErrors.UseDPoPNonce || response.Error == OidcConstants.TokenErrors.InvalidDPoPProof) &&
            dPoPJsonWebKey != null &&
            response.DPoPNonce != null)
        {
            logger.DPoPErrorDuringTokenRefreshWillRetryWithServerNonce(response.ErrorDescription);

            var proof = await dPoPProofService.CreateProofTokenAsync(new DPoPProofRequest
            {
                Url = tokenEndpoint,
                Method = HttpMethod.Post,
                DPoPJsonWebKey = dPoPJsonWebKey,
                DPoPNonce = DPoPNonce.ParseOrDefault(response.DPoPNonce)
            });

            request.DPoPProofToken = proof?.ProofToken.Value;

            if (request.DPoPProofToken != null)
            {
                metrics.DPoPNonceErrorRetry(request.ClientId, AccessTokenManagementMetrics.TokenRequestType.User, response.Error);
                response = await oidc.HttpClient!.RequestRefreshTokenAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }

        if (response.IsError)
        {
            logger.FailedToRefreshAccessToken(response.Error, response.ErrorDescription);
            metrics.TokenRetrievalFailed(request.ClientId, AccessTokenManagementMetrics.TokenRequestType.User, response.Error);
            return TokenResult.Failure(response.Error ?? "Failed to acquire access token", response.ErrorDescription);
        }

        metrics.TokenRetrieved(request.ClientId, AccessTokenManagementMetrics.TokenRequestType.User);
        var token = new UserToken()
        {
            IdentityToken = IdentityTokenString.ParseOrDefault(response.IdentityToken),
            AccessToken = AccessTokenString.Parse(response.AccessToken ??
                                                  throw new InvalidOperationException("No access token present")),
            AccessTokenType = AccessTokenType.ParseOrDefault(response.TokenType),
            DPoPJsonWebKey = dPoPJsonWebKey,
            Expiration = response.ExpiresIn == 0
                ? DateTimeOffset.MaxValue
                : DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn),
            RefreshToken = response.RefreshToken == null
                ? refreshToken.RefreshToken // use input refresh token if none is returned
                : RefreshTokenString.Parse(response.RefreshToken),
            Scope = Scope.ParseOrDefault(response.Scope),
            ClientId = oidc.ClientId
        };

        logger.UserAccessTokenRefreshed(token.AccessTokenType, token.Expiration);
        return token;
    }

    /// <inheritdoc/>
    public async Task RevokeRefreshTokenAsync(UserRefreshToken userToken,
        UserTokenRequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var refreshToken = userToken.RefreshToken ?? throw new ArgumentNullException(nameof(userToken.RefreshToken));

        logger.RevokingRefreshToken(refreshToken, hashAlgorithm: Crypto.HashData);

        var oidc = await configurationService.GetOpenIdConnectConfigurationAsync(parameters.ChallengeScheme).ConfigureAwait(false);

        var revocationEndpoint = oidc.RevocationEndpoint ??
                                 throw new InvalidOperationException("Revocation endpoint is null");

        var request = new TokenRevocationRequest
        {
            Address = revocationEndpoint.ToString(),

            ClientId = oidc.ClientId?.Value ?? throw new InvalidOperationException("ClientID is null"),
            ClientSecret = oidc.ClientSecret?.Value ?? throw new InvalidOperationException("ClientSecret is null"),
            ClientCredentialStyle = options.Value.ClientCredentialStyle,

            Token = refreshToken.Value,
            TokenTypeHint = OidcConstants.TokenTypes.RefreshToken
        };

        request.Options.TryAdd(ClientCredentialsTokenManagementDefaults.TokenRequestParametersOptionsName, parameters);

        if (parameters.Assertion != null)
        {
            request.ClientAssertion = parameters.Assertion;
            request.ClientCredentialStyle = ClientCredentialStyle.PostBody;
        }
        else
        {
            var assertion = await clientAssertionService.GetClientAssertionAsync(OpenIdConnectTokenManagementDefaults.ClientCredentialsClientNamePrefix + oidc.Scheme, parameters).ConfigureAwait(false);
            if (assertion != null)
            {
                request.ClientAssertion = assertion;
                request.ClientCredentialStyle = ClientCredentialStyle.PostBody;
            }
        }

        logger.SendingTokenRevocationRequest(revocationEndpoint);
        var response = await oidc.HttpClient!.RevokeTokenAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.IsError)
        {
            logger.FailedToRevokeAccessToken(response.Error);
        }
    }
}
