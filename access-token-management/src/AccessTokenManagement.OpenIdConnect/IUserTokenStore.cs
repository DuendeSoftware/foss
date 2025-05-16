// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Claims;


namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Storage abstraction for access and refresh tokens
/// </summary>
public interface IUserTokenStore
{
    /// <summary>
    /// Stores tokens
    /// </summary>
    /// <param name="user">User the tokens belong to</param>
    /// <param name="token"></param>
    /// <param name="parameters">Extra optional parameters</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task StoreTokenAsync(
        ClaimsPrincipal user,
        UserToken token,
        UserTokenRequestParameters? parameters = null,
        CT ct = default);

    /// <summary>
    /// Retrieves tokens from store
    /// </summary>
    /// <param name="user">User the tokens belong to</param>
    /// <param name="parameters">Extra optional parameters</param>
    /// <param name="ct"></param>
    /// <returns>access and refresh token and access token expiration</returns>
    Task<TokenResult<TokenForParameters>> GetTokenAsync(
        ClaimsPrincipal user,
        UserTokenRequestParameters? parameters = null,
        CT ct = default);

    /// <summary>
    /// Clears the stored tokens for a given user
    /// </summary>
    /// <param name="user">User the tokens belong to</param>
    /// <param name="parameters">Extra optional parameters</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ClearTokenAsync(
        ClaimsPrincipal user,
        UserTokenRequestParameters? parameters = null,
        CT ct = default);
}
