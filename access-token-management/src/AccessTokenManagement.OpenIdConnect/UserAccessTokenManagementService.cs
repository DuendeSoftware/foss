// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.AccessTokenManagement.OTel;
using Duende.IdentityModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Implements basic token management logic
/// </summary>
public class UserAccessAccessTokenManagementService(
    AccessTokenManagementMetrics metrics,
    IUserTokenRequestSynchronization sync,
    IUserTokenStore userAccessTokenStore,
    TimeProvider clock,
    IOptions<UserTokenManagementOptions> options,
    IUserTokenEndpointService tokenEndpointService,
    ILogger<UserAccessAccessTokenManagementService> logger) : IUserTokenManagementService
{

    /// <inheritdoc/>
    public async Task<UserToken> GetAccessTokenAsync(
        ClaimsPrincipal user,
        UserTokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        logger.StartingUserTokenAcquisition();

        parameters ??= new UserTokenRequestParameters();

        if (!user.Identity!.IsAuthenticated)
        {
            logger.CannotRetrieveAccessTokenDueToNoActiveUser();
            return new UserToken() { Error = "No active user" };
        }

        var userName = user.FindFirst(JwtClaimTypes.Name)?.Value ??
                       user.FindFirst(JwtClaimTypes.Subject)?.Value ?? "unknown";
        var userToken = await userAccessTokenStore.GetTokenAsync(user, parameters).ConfigureAwait(false);

        if (userToken.AccessToken.IsMissing() && userToken.RefreshToken.IsMissing())
        {
            logger.CannotRetrieveAccessTokenDueToNoTokenDataFound(userName);
            return new UserToken() { Error = "No token data for user" };
        }

        if (userToken.AccessToken.IsPresent() && userToken.RefreshToken.IsMissing())
        {
            logger.CannotRetrieveAccessTokenDueToNoRefreshTokenFound(userName, parameters.Resource ?? "default");
            return userToken;
        }

        var needsRenewal = userToken.AccessToken.IsMissing() && userToken.RefreshToken.IsPresent();
        if (needsRenewal)
        {
            logger.NoAccessTokenFoundWillRefresh(userName, parameters.Resource ?? "default");
        }

        var dtRefresh = userToken.Expiration.Subtract(options.Value.RefreshBeforeExpiration);
        var utcNow = clock.GetUtcNow();
        if (dtRefresh < utcNow || parameters.ForceRenewal || needsRenewal)
        {
            logger.DebugTokenNeedsRefreshing(userName, dtRefresh, parameters.ForceRenewal);

            return await sync.SynchronizeAsync(userToken.RefreshToken!, async () =>
            {
                var token = await RefreshUserAccessTokenAsync(user, parameters, cancellationToken).ConfigureAwait(false);

                if (!token.IsError)
                {
                    logger.ReturningRefreshedToken(userName);
                }

                return token;
            }).ConfigureAwait(false);
        }
        else
        {
            logger.LogTrace("Returning current token for user: {user}", userName);
        }

        logger.ReturningCurrentTokenForUser(userName);
        metrics.AccessTokenUsed(userToken.ClientId, AccessTokenManagementMetrics.TokenRequestType.User);
        return userToken;
    }

    /// <inheritdoc/>
    public async Task RevokeRefreshTokenAsync(
        ClaimsPrincipal user,
        UserTokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        parameters ??= new UserTokenRequestParameters();
        var userToken = await userAccessTokenStore.GetTokenAsync(user, parameters).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(userToken.RefreshToken))
        {
            await tokenEndpointService.RevokeRefreshTokenAsync(userToken, parameters, cancellationToken).ConfigureAwait(false);
            await userAccessTokenStore.ClearTokenAsync(user, parameters).ConfigureAwait(false);
        }
    }

    private async Task<UserToken> RefreshUserAccessTokenAsync(
        ClaimsPrincipal user,
        UserTokenRequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var userToken = await userAccessTokenStore.GetTokenAsync(user, parameters).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(userToken.RefreshToken))
        {
            throw new InvalidOperationException("No refresh token in store.");
        }

        var refreshedToken =
            await tokenEndpointService.RefreshAccessTokenAsync(userToken, parameters, cancellationToken).ConfigureAwait(false);
        if (!refreshedToken.IsError)
        {
            await userAccessTokenStore.StoreTokenAsync(user, refreshedToken, parameters).ConfigureAwait(false);
        }

        return refreshedToken;
    }
}
