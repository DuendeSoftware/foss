// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.AccessTokenManagement.Internal;
using Duende.AccessTokenManagement.OTel;
using Duende.AccessTokenManagement.Types;
using Duende.IdentityModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

/// <summary>
/// Implements basic token management logic
/// </summary>
internal class UserAccessAccessTokenManagementService(
    AccessTokenManagementMetrics metrics,
    IUserTokenRequestSynchronization sync,
    IUserTokenStore userAccessTokenStore,
    TimeProvider clock,
    IOptions<UserTokenManagementOptions> options,
    IUserTokenEndpointService tokenEndpointService,
    ILogger<UserAccessAccessTokenManagementService> logger) : IUserTokenManagementService
{

    /// <inheritdoc/>
    public async Task<TokenResult<UserToken>> GetAccessTokenAsync(
        ClaimsPrincipal user,
        UserTokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        logger.StartingUserTokenAcquisition();

        parameters ??= new UserTokenRequestParameters();

        if (!user.Identity!.IsAuthenticated)
        {
            logger.CannotRetrieveAccessTokenDueToNoActiveUser();
            return TokenResult.Failure("No active user");
        }

        var userName = user.FindFirst(JwtClaimTypes.Name)?.Value ??
                       user.FindFirst(JwtClaimTypes.Subject)?.Value ?? "unknown";
        var getTokenForSpecificParameters = await userAccessTokenStore.GetTokenAsync(user, parameters, cancellationToken).ConfigureAwait(false);

        if (!getTokenForSpecificParameters.WasSuccessful(out var requestedToken, out var failure))
        {
            return failure;
        }

        if (requestedToken.NoRefreshToken)
        {
            // Todo: EV: Change this log message
            logger.CannotRetrieveAccessTokenDueToNoRefreshTokenFound(userName, parameters.Resource ?? "default");
            return requestedToken.TokenForSpecifiedParameters;
        }

        TokenResult<UserToken> result;

        var refreshAfter = clock.GetUtcNow() + options.Value.RefreshBeforeExpiration;

        if (parameters.ForceTokenRenewal.Value || // We must refresh the token
            requestedToken.TokenForSpecifiedParameters == null || // There is no token for the current specified set of parameters
            requestedToken.TokenForSpecifiedParameters.Expiration < refreshAfter) // The existing token is expired
        {
            logger.DebugTokenNeedsRefreshing(userName, requestedToken.TokenForSpecifiedParameters?.Expiration, parameters.ForceTokenRenewal);

            result = await sync.SynchronizeAsync(requestedToken.RefreshToken, async () =>
            {
                var getRefreshedToken = await tokenEndpointService.RefreshAccessTokenAsync(
                            requestedToken.RefreshToken,
                            parameters,
                            cancellationToken).ConfigureAwait(false);

                if (!getRefreshedToken.WasSuccessful(out var refreshedToken, out var refreshError))
                {
                    return refreshError;
                }

                await userAccessTokenStore.StoreTokenAsync(user, refreshedToken, parameters, cancellationToken).ConfigureAwait(false);
                logger.ReturningRefreshedToken(userName);

                return getRefreshedToken;
            }, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            logger.LogTrace("Returning current token for user: {user}", userName);
            result = requestedToken.TokenForSpecifiedParameters;
        }

        if (!result.WasSuccessful(out var refreshedToken, out var error))
        {
            return error;
        }

        logger.ReturningCurrentTokenForUser(userName);
        metrics.AccessTokenUsed(refreshedToken.ClientId, AccessTokenManagementMetrics.TokenRequestType.User);
        return refreshedToken;
    }

    /// <inheritdoc/>
    public async Task RevokeRefreshTokenAsync(
        ClaimsPrincipal user,
        UserTokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        parameters ??= new UserTokenRequestParameters();
        var getToken = await userAccessTokenStore.GetTokenAsync(user, parameters, cancellationToken).ConfigureAwait(false);

        if (getToken.WasSuccessful(out var userToken) && userToken.RefreshToken != null)
        {
            await tokenEndpointService.RevokeRefreshTokenAsync(userToken.RefreshToken, parameters, cancellationToken).ConfigureAwait(false);
            await userAccessTokenStore.ClearTokenAsync(user, parameters, cancellationToken).ConfigureAwait(false);
        }
    }
}
