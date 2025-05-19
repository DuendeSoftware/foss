// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Authentication;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Interface that encapsulates the logic of storing UserTokens in AuthenticationProperties
/// </summary>
public interface IStoreTokensInAuthenticationProperties
{
    /// <summary>
    /// Gets a UserToken from the AuthenticationProperties
    /// </summary>
    TokenResult<TokenForParameters> GetUserToken(AuthenticationProperties authenticationProperties, UserTokenRequestParameters? parameters = null);

    /// <summary>
    /// Sets a UserToken in the AuthenticationProperties.
    /// </summary>
    Task SetUserTokenAsync(UserToken token, AuthenticationProperties authenticationProperties,
        UserTokenRequestParameters? parameters = null,
        CT ct = default);

    /// <summary>
    /// Removes a UserToken from the AuthenticationProperties.
    /// </summary>
    /// <param name="authenticationProperties"></param>
    /// <param name="parameters"></param>
    void RemoveUserToken(AuthenticationProperties authenticationProperties, UserTokenRequestParameters? parameters = null);

    /// <summary>
    /// Gets the scheme name used when storing a UserToken in an
    /// AuthenticationProperties.
    /// </summary>
    Task<Scheme> GetSchemeAsync(UserTokenRequestParameters? parameters = null,
        CT ct = default);
}
