// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Implements token endpoint operations using IdentityModel
/// </summary>
public class UserTokenEndpointService(
    IOpenIdConnectConfigurationService configurationService,
    IOptions<UserTokenManagementOptions> options,
    IClientAssertionService clientAssertionService,
    IDPoPProofService dPoPProofService,
    ILogger<UserTokenEndpointService> logger) : IUserTokenEndpointService
{
    /// <inheritdoc/>
    public async Task<UserToken> RefreshAccessTokenAsync(
        UserToken userToken,
        UserTokenRequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var refreshToken = userToken.RefreshToken ?? throw new ArgumentNullException(nameof(userToken.RefreshToken));
        
        logger.RefreshingAccessTokenUsingRefreshToken(refreshToken, hashAlgorithm: Crypto.HashData);

        var oidc = await configurationService.GetOpenIdConnectConfigurationAsync(parameters.ChallengeScheme).ConfigureAwait(false);

        var request = new RefreshTokenRequest
        {
            Address = oidc.TokenEndpoint,

            ClientId = oidc.ClientId!,
            ClientSecret = oidc.ClientSecret,
            ClientCredentialStyle = options.Value.ClientCredentialStyle,

            RefreshToken = refreshToken
        };

        request.Options.TryAdd(ClientCredentialsTokenManagementDefaults.TokenRequestParametersOptionsName, parameters);

        if (!string.IsNullOrWhiteSpace(parameters.Scope))
        {
            request.Scope = parameters.Scope;
        }

        if (!string.IsNullOrEmpty(parameters.Resource))
        {
            request.Resource.Add(parameters.Resource);
        }

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

        var dPoPJsonWebKey = userToken.DPoPJsonWebKey;
        if (dPoPJsonWebKey != null)
        {
            var proof = await dPoPProofService.CreateProofTokenAsync(new DPoPProofRequest
            {
                Url = request.Address!,
                Method = "POST",
                DPoPJsonWebKey = dPoPJsonWebKey,
            });
            request.DPoPProofToken = proof?.ProofToken;
        }

        logger.SendingRefreshTokenRequest(request.Address);
        var response = await oidc.HttpClient!.RequestRefreshTokenAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.IsError &&
            (response.Error == OidcConstants.TokenErrors.UseDPoPNonce || response.Error == OidcConstants.TokenErrors.InvalidDPoPProof) &&
            dPoPJsonWebKey != null &&
            response.DPoPNonce != null)
        {
            logger.DPoPErrorDuringTokenRefreshWillRetryWithServerNonce();

            var proof = await dPoPProofService.CreateProofTokenAsync(new DPoPProofRequest
            {
                Url = request.Address!,
                Method = "POST",
                DPoPJsonWebKey = dPoPJsonWebKey,
                DPoPNonce = response.DPoPNonce
            });
            request.DPoPProofToken = proof?.ProofToken;

            if (request.DPoPProofToken != null)
            {
                response = await oidc.HttpClient!.RequestRefreshTokenAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }

        var token = new UserToken();
        if (response.IsError)
        {
            token.Error = response.Error;
        }
        else
        {
            token.IdentityToken = response.IdentityToken;
            token.AccessToken = response.AccessToken;
            token.AccessTokenType = response.TokenType;
            token.DPoPJsonWebKey = dPoPJsonWebKey;
            token.Expiration = response.ExpiresIn == 0
                ? DateTimeOffset.MaxValue
                : DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn);
            token.RefreshToken = response.RefreshToken ?? userToken.RefreshToken;
            token.Scope = response.Scope;
        }

        return token;
    }

    /// <inheritdoc/>
    public async Task RevokeRefreshTokenAsync(
        UserToken userToken,
        UserTokenRequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var refreshToken = userToken.RefreshToken ?? throw new ArgumentNullException(nameof(userToken.RefreshToken));

        logger.RevokingRefreshToken(refreshToken, hashAlgorithm: Crypto.HashData);

        var oidc = await configurationService.GetOpenIdConnectConfigurationAsync(parameters.ChallengeScheme).ConfigureAwait(false);

        if (string.IsNullOrEmpty(oidc.RevocationEndpoint))
        {
            throw new InvalidOperationException("Revocation endpoint not configured");
        }

        var request = new TokenRevocationRequest
        {
            Address = oidc.RevocationEndpoint,

            ClientId = oidc.ClientId!,
            ClientSecret = oidc.ClientSecret,
            ClientCredentialStyle = options.Value.ClientCredentialStyle,

            Token = refreshToken,
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

        logger.SendingTokenRevocationRequest(request.Address);
        var response = await oidc.HttpClient!.RevokeTokenAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.IsError)
        {
            logger.FailedToRevokeAccessToken(response.Error);
        }
    }
}
