// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace Duende.AccessTokenManagement.Framework;

public abstract class IntegrationTestBase : IAsyncDisposable
{
    public TestData The { get; } = new();
    public TestDataBuilder Some => new(The);

    protected readonly IdentityServerHost IdentityServerHost;
    protected readonly ApiHost ApiHost;
    protected readonly AppHost AppHost;

    protected IntegrationTestBase(ITestOutputHelper output, string clientId = "web", Action<UserTokenManagementOptions>? configureUserTokenManagementOptions = null)
    {
        IdentityServerHost = new IdentityServerHost(output.WriteLine);

        IdentityServerHost.Clients.Add(new Client
        {
            ClientId = "client_credentials_client",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = { "scope1" }
        });

        IdentityServerHost.Clients.Add(new Client
        {
            ClientId = "web",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
            RedirectUris = { "https://app/signin-oidc" },
            PostLogoutRedirectUris = { "https://app/signout-callback-oidc" },
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "profile", "scope1" }
        });

        IdentityServerHost.Clients.Add(new Client
        {
            ClientId = "web.short",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
            RedirectUris = { "https://app/signin-oidc" },
            PostLogoutRedirectUris = { "https://app/signout-callback-oidc" },
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "profile", "scope1" },

            AccessTokenLifetime = 10
        });

        IdentityServerHost.Clients.Add(new Client
        {
            ClientId = "dpop",
            ClientSecrets = { new Secret("secret".ToSha256()) },
            AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
            RedirectUris = { "https://app/signin-oidc" },
            PostLogoutRedirectUris = { "https://app/signout-callback-oidc" },
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "profile", "scope1" },

            RequireDPoP = true,
            DPoPValidationMode = DPoPTokenExpirationValidationMode.Nonce,
            DPoPClockSkew = TimeSpan.FromMilliseconds(10),

            AccessTokenLifetime = 10
        });

        ApiHost = new ApiHost(output.WriteLine, IdentityServerHost, "scope1");

        AppHost = new AppHost(output.WriteLine, IdentityServerHost, ApiHost, clientId, configureUserTokenManagementOptions: configureUserTokenManagementOptions);
    }

    public virtual async ValueTask DisposeAsync()
    {
        await IdentityServerHost.DisposeAsync();
        await ApiHost.DisposeAsync();
        await AppHost.DisposeAsync();
    }

    public virtual async ValueTask InitializeAsync()
    {
        await ApiHost.InitializeAsync();
        await AppHost.InitializeAsync();
        await IdentityServerHost.InitializeAsync();
    }
}
