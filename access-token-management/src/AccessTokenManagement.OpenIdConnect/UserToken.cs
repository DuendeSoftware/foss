// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Models a user access token
/// </summary>
public class UserToken : ClientCredentialsToken
{
    /// <summary>
    /// The refresh token
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// The identity token that may be populated by the OP when refreshing the access token. This
    /// value is not stored, but available should some OP's require to send this value, for example
    /// during logout. 
    /// </summary>
    public string? IdentityToken { get; set; }
}