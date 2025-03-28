// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Implements basic token management logic
/// </summary>
public class UserAccessAccessTokenManagementService : IUserTokenManagementService
{
    private readonly IUserTokenRequestSynchronization _sync;
    private readonly IUserTokenStore _userAccessTokenStore;
    private readonly TimeProvider _clock;
    private readonly UserTokenManagementOptions _options;
    private readonly IUserTokenEndpointService _tokenEndpointService;
    private readonly ILogger<UserAccessAccessTokenManagementService> _logger;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="sync"></param>
    /// <param name="userAccessTokenStore"></param>
    /// <param name="clock"></param>
    /// <param name="options"></param>
    /// <param name="tokenEndpointService"></param>
    /// <param name="logger"></param>
    public UserAccessAccessTokenManagementService(
        IUserTokenRequestSynchronization sync,
        IUserTokenStore userAccessTokenStore,
        TimeProvider clock,
        IOptions<UserTokenManagementOptions> options,
        IUserTokenEndpointService tokenEndpointService,
        ILogger<UserAccessAccessTokenManagementService> logger)
    {
        _sync = sync;
        _userAccessTokenStore = userAccessTokenStore;
        _clock = clock;
        _options = options.Value;
        _tokenEndpointService = tokenEndpointService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UserToken> GetAccessTokenAsync(
        ClaimsPrincipal user,
        UserTokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        _logger.TraceStartingUserTokenAcquisition();

        parameters ??= new UserTokenRequestParameters();

        if (!user.Identity!.IsAuthenticated)
        {
            _logger.DebugNoActiveUser();
            return new UserToken() { Error = "No active user" };
        }

        var userName = user.FindFirst(JwtClaimTypes.Name)?.Value ??
                       user.FindFirst(JwtClaimTypes.Subject)?.Value ?? "unknown";
        var userToken = await _userAccessTokenStore.GetTokenAsync(user, parameters).ConfigureAwait(false);

        if (userToken.AccessToken.IsMissing() && userToken.RefreshToken.IsMissing())
        {
            _logger.DebugNoTokenDataFound(userName);
            return new UserToken() { Error = "No token data for user" };
        }

        if (userToken.AccessToken.IsPresent() && userToken.RefreshToken.IsMissing())
        {
            _logger.DebugNoRefreshTokenFound(userName, parameters.Resource ?? "default");
            return userToken;
        }

        var needsRenewal = userToken.AccessToken.IsMissing() && userToken.RefreshToken.IsPresent();
        if (needsRenewal)
        {
            _logger.DebugNoAccessTokenFound(userName, parameters.Resource ?? "default");
        }

        var dtRefresh = userToken.Expiration.Subtract(_options.RefreshBeforeExpiration);
        var utcNow = _clock.GetUtcNow();
        if (dtRefresh < utcNow || parameters.ForceRenewal || needsRenewal)
        {
            _logger.DebugTokenNeedsRefreshing(userName);

            return await _sync.SynchronizeAsync(userToken.RefreshToken!, async () =>
            {
                var token = await RefreshUserAccessTokenAsync(user, parameters, cancellationToken).ConfigureAwait(false);

                if (!token.IsError)
                {
                    _logger.TraceReturningRefreshedToken(userName);
                }

                return token;
            }).ConfigureAwait(false);
        }

        _logger.TraceReturningCurrentToken(userName);
        return userToken;
    }

    /// <inheritdoc/>
    public async Task RevokeRefreshTokenAsync(
        ClaimsPrincipal user,
        UserTokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        parameters ??= new UserTokenRequestParameters();
        var userToken = await _userAccessTokenStore.GetTokenAsync(user, parameters).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(userToken.RefreshToken))
        {
            await _tokenEndpointService.RevokeRefreshTokenAsync(userToken, parameters, cancellationToken).ConfigureAwait(false);
            await _userAccessTokenStore.ClearTokenAsync(user, parameters).ConfigureAwait(false);
        }
    }

    private async Task<UserToken> RefreshUserAccessTokenAsync(
        ClaimsPrincipal user,
        UserTokenRequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var userToken = await _userAccessTokenStore.GetTokenAsync(user, parameters).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(userToken.RefreshToken))
        {
            throw new InvalidOperationException("No refresh token in store.");
        }

        var refreshedToken =
            await _tokenEndpointService.RefreshAccessTokenAsync(userToken, parameters, cancellationToken).ConfigureAwait(false);
        if (refreshedToken.IsError)
        {
            _logger.ErrorRefreshingAccessToken(refreshedToken.Error);
        }
        else
        {
            await _userAccessTokenStore.StoreTokenAsync(user, refreshedToken, parameters).ConfigureAwait(false);
        }

        return refreshedToken;
    }
}
