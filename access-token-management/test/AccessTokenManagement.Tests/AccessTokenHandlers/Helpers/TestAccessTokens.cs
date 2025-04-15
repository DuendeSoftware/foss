// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;

namespace Duende.AccessTokenManagement.AccessTokenHandlers.Helpers;

public class TestAccessTokens(string? dPoPJsonWebKey)
{
    public UserToken UserToken =
        new UserToken()
        {
            ClientId = "clientId",
            IdentityToken = "identity_token",
            AccessToken = "access_token_1",
            AccessTokenType = "Bearer",
            Expiration = DateTimeOffset.UtcNow.AddMinutes(5),
            RefreshToken = "refresh_token",
            DPoPJsonWebKey = dPoPJsonWebKey
        };
}
