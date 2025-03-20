// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace Perf.IdentityServer;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
        new IdentityResource[]
        {
            new IdentityResources.OpenId()
        };

    public static IEnumerable<ApiScope> ApiScopes =>
        new ApiScope[]
        {
            new("api", ["name"]),
        };

    public static IEnumerable<Client> Clients
    {
        get
        {
            var tokenEndpointUrl = Services.TokenEndpoint.ActualUri();

            return new Client[]
            {
                new Client()
                {
                    ClientId = "tokenendpoint",
                    ClientSecrets = { new Secret("secret".Sha256()) },

                    AllowedGrantTypes =
                    {
                        GrantType.AuthorizationCode,
                        GrantType.ClientCredentials,
                        OidcConstants.GrantTypes.TokenExchange
                    },

                    RedirectUris = { $"{tokenEndpointUrl}signin-oidc" },
                    FrontChannelLogoutUri = $"{tokenEndpointUrl}signout-oidc",
                    PostLogoutRedirectUris = { $"{tokenEndpointUrl}signout-callback-oidc" },

                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "api", "scope-for-isolated-api" },

                    RefreshTokenExpiration = TokenExpiration.Absolute,
                    AbsoluteRefreshTokenLifetime = 100,
                    AccessTokenLifetime = 2
                }
            };
        }
    }
}
