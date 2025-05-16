// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.



namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Abstraction for token endpoint operations
/// </summary>
public interface IOpenIdConnectUserTokenEndpoint
{
    /// <summary>
    /// Refreshes a user access token.
    /// </summary>
    /// <param name="userToken"></param>
    /// <param name="parameters"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<TokenResult<UserToken>> RefreshAccessTokenAsync(
        UserRefreshToken userToken,
        UserTokenRequestParameters parameters,
        CT ct = default);

    /// <summary>
    /// Revokes a refresh token.
    /// </summary>
    /// <param name="userToken"></param>
    /// <param name="parameters"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task RevokeRefreshTokenAsync(UserRefreshToken userToken,
        UserTokenRequestParameters parameters,
        CT ct = default);
}

