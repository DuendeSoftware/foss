// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.AccessTokenManagement.Internal;
using Duende.AccessTokenManagement.OTel;

using Duende.IdentityModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

/// <summary>
/// Implements basic token management logic
/// </summary>
internal class UserAccessAccessTokenManager(
    AccessTokenManagementMetrics metrics,
    IUserTokenRequestConcurrencyControl sync,
    IUserTokenStore userAccessTokenStore,
    TimeProvider clock,
    IOptions<UserTokenManagementOptions> options,
    IOpenIdConnectUserTokenEndpoint tokenClient,
    ILogger<UserAccessAccessTokenManager> logger) : IUserTokenManager
{

    /// <inheritdoc/>
    public async Task<TokenResult<UserToken>> GetAccessTokenAsync(
        ClaimsPrincipal user,
        UserTokenRequestParameters? parameters = null,
        CT ct = default)
    {
        logger.StartingUserTokenAcquisition(LogLevel.Debug);

        parameters ??= new UserTokenRequestParameters();

        if (!user.Identity!.IsAuthenticated)
        {
            logger.CannotRetrieveAccessTokenDueToNoActiveUser(LogLevel.Error);
            return TokenResult.Failure("No active user");
        }

        var userName = user.FindFirst(JwtClaimTypes.Name)?.Value ??
                       user.FindFirst(JwtClaimTypes.Subject)?.Value ?? "unknown";
        var getTokenForSpecificParameters = await userAccessTokenStore.GetTokenAsync(user, parameters, ct).ConfigureAwait(false);

        if (!getTokenForSpecificParameters.WasSuccessful(out var requestedToken, out var failure))
        {
            return failure;
        }

        if (requestedToken.NoRefreshToken)
        {
            logger.NoRefreshTokenAvailableWillNotRefresh(LogLevel.Debug, userName, parameters.Resource ?? "default");
            return requestedToken.TokenForSpecifiedParameters;
        }

        TokenResult<UserToken> result;

        var refreshAfter = clock.GetUtcNow() + options.Value.RefreshBeforeExpiration;

        var shouldRefresh = parameters.ForceTokenRenewal.Value     // We must refresh the token
                            || requestedToken.TokenForSpecifiedParameters == null   // Or there is no token for the current specified set of parameters
                            || requestedToken.TokenForSpecifiedParameters.Expiration < refreshAfter; // Or the existing token is expired

        if (shouldRefresh)
        {
            logger.TokenNeedsRefreshing(LogLevel.Debug, userName, requestedToken.TokenForSpecifiedParameters?.Expiration, parameters.ForceTokenRenewal);

            // Synchronize access to the token request, meaning multiple concurrent requests
            // for the same token will be grouped together. 
            result = await sync.ExecuteWithConcurrencyControlAsync(
                key: requestedToken.RefreshToken,
                tokenRetriever: async () =>
                {
                    var getRefreshedToken = await tokenClient.RefreshAccessTokenAsync(
                                requestedToken.RefreshToken,
                                parameters,
                                ct).ConfigureAwait(false);

                    if (!getRefreshedToken.WasSuccessful(out var token, out var refreshError))
                    {
                        return refreshError;
                    }

                    await userAccessTokenStore.StoreTokenAsync(user, token, parameters, ct).ConfigureAwait(false);
                    logger.ReturningRefreshedToken(LogLevel.Trace, userName);

                    return getRefreshedToken;
                },
                ct: ct).ConfigureAwait(false);
        }
        else
        {
            // No refresh will be done. 
            if (requestedToken.TokenForSpecifiedParameters == null)
            {
                return TokenResult.Failure("No access token was found nor a refresh token.");
            }

            logger.ReturningCurrentTokenForUser(LogLevel.Trace, userName);
            result = requestedToken.TokenForSpecifiedParameters;
        }

        if (!result.WasSuccessful(out var refreshedToken, out var error))
        {
            return error;
        }

        metrics.AccessTokenUsed(refreshedToken.ClientId, AccessTokenManagementMetrics.TokenRequestType.User);
        return refreshedToken;
    }

    /// <inheritdoc/>
    public async Task RevokeRefreshTokenAsync(
        ClaimsPrincipal user,
        UserTokenRequestParameters? parameters = null,
        CT ct = default)
    {
        parameters ??= new UserTokenRequestParameters();
        var getToken = await userAccessTokenStore.GetTokenAsync(user, parameters, ct).ConfigureAwait(false);

        if (getToken.WasSuccessful(out var userToken) && userToken.RefreshToken != null)
        {
            await tokenClient.RevokeRefreshTokenAsync(userToken.RefreshToken, parameters, ct).ConfigureAwait(false);
            await userAccessTokenStore.ClearTokenAsync(user, parameters, ct).ConfigureAwait(false);
        }
    }
}
