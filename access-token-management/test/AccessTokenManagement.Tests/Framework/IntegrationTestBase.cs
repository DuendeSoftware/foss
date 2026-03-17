// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using JsonWebKey = Microsoft.IdentityModel.Tokens.JsonWebKey;

namespace Duende.AccessTokenManagement.Framework;

public abstract class IntegrationTestBase : IAsyncDisposable
{
    public TestData The { get; } = new();
    public TestDataBuilder Some => new(The);
    protected readonly IdentityServerHost IdentityServerHost;
    protected readonly ApiHost ApiHost;
    protected readonly AppHost AppHost;

    protected IntegrationTestBase(
        ITestOutputHelper output,
        string clientId = "web",
        Action<UserTokenManagementOptions>? configureUserTokenManagementOptions = null)
    {
        IdentityServerHost = new IdentityServerHost(output.WriteLine);

        IdentityServerHost.Clients.Add(new Client
        {
            ClientId = "client_credentials_client",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = { "scope1", "scope2" }
        });

        IdentityServerHost.Clients.Add(new Client
        {
            ClientId = "web",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
            RedirectUris = { "https://app/signin-oidc" },
            PostLogoutRedirectUris = { "https://app/signout-callback-oidc" },
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "profile", "scope1", "scope2" }
        });

        IdentityServerHost.Clients.Add(new Client
        {
            ClientId = "web.short",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
            RedirectUris = { "https://app/signin-oidc" },
            PostLogoutRedirectUris = { "https://app/signout-callback-oidc" },
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "profile", "scope1", "scope2" },

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
            AllowedScopes = { "openid", "profile", "scope1", "scope2" },

            RequireDPoP = true,
            DPoPValidationMode = DPoPTokenExpirationValidationMode.Nonce,
            DPoPClockSkew = TimeSpan.FromMilliseconds(10),

            AccessTokenLifetime = 10
        });

        IdentityServerHost.Clients.Add(new Client
        {
            ClientId = "dpop-assertion",
            ClientSecrets =
            {
                new Secret
                {
                    Type = IdentityServerConstants.SecretTypes.JsonWebKey,
                    Value = BuildPublicJwk(ClientAssertionPrivateJwk)
                }
            },
            AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
            RedirectUris = { "https://app/signin-oidc" },
            PostLogoutRedirectUris = { "https://app/signout-callback-oidc" },
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "profile", "scope1", "scope2" },
            RequireDPoP = true,
            DPoPValidationMode = DPoPTokenExpirationValidationMode.Nonce,
            DPoPClockSkew = TimeSpan.FromMilliseconds(10),
            AccessTokenLifetime = 10
        });

        IdentityServerHost.Clients.Add(new Client
        {
            ClientId = "par-assertion",
            ClientSecrets =
            {
                new Secret
                {
                    Type = IdentityServerConstants.SecretTypes.JsonWebKey,
                    Value = BuildPublicJwk(ClientAssertionPrivateJwk)
                }
            },
            AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
            RedirectUris = { "https://app/signin-oidc" },
            PostLogoutRedirectUris = { "https://app/signout-callback-oidc" },
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "profile", "scope1", "scope2" },
            RequirePushedAuthorization = true,
            AccessTokenLifetime = 10
        });

        IdentityServerHost.Clients.Add(new Client
        {
            ClientId = "par-dpop-assertion",
            ClientSecrets =
            {
                new Secret
                {
                    Type = IdentityServerConstants.SecretTypes.JsonWebKey,
                    Value = BuildPublicJwk(ClientAssertionPrivateJwk)
                }
            },
            AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
            RedirectUris = { "https://app/signin-oidc" },
            PostLogoutRedirectUris = { "https://app/signout-callback-oidc" },
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "profile", "scope1", "scope2" },
            RequirePushedAuthorization = true,
            RequireDPoP = true,
            DPoPValidationMode = DPoPTokenExpirationValidationMode.Nonce,
            DPoPClockSkew = TimeSpan.FromMilliseconds(10),
            AccessTokenLifetime = 10
        });

        ApiHost = new ApiHost(output.WriteLine, IdentityServerHost, ["scope1", "scope2"]);
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

    protected const string ClientAssertionPrivateJwk =
        """
        {
            "d":"GmiaucNIzdvsEzGjZjd43SDToy1pz-Ph-shsOUXXh-dsYNGftITGerp8bO1iryXh_zUEo8oDK3r1y4klTonQ6bLsWw4ogjLPmL3yiqsoSjJa1G2Ymh_RY_sFZLLXAcrmpbzdWIAkgkHSZTaliL6g57vA7gxvd8L4s82wgGer_JmURI0ECbaCg98JVS0Srtf9GeTRHoX4foLWKc1Vq6NHthzqRMLZe-aRBNU9IMvXNd7kCcIbHCM3GTD_8cFj135nBPP2HOgC_ZXI1txsEf-djqJj8W5vaM7ViKU28IDv1gZGH3CatoysYx6jv1XJVvb2PH8RbFKbJmeyUm3Wvo-rgQ",
            "dp":"YNjVBTCIwZD65WCht5ve06vnBLP_Po1NtL_4lkholmPzJ5jbLYBU8f5foNp8DVJBdFQW7wcLmx85-NC5Pl1ZeyA-Ecbw4fDraa5Z4wUKlF0LT6VV79rfOF19y8kwf6MigyrDqMLcH_CRnRGg5NfDsijlZXffINGuxg6wWzhiqqE",
            "dq":"LfMDQbvTFNngkZjKkN2CBh5_MBG6Yrmfy4kWA8IC2HQqID5FtreiY2MTAwoDcoINfh3S5CItpuq94tlB2t-VUv8wunhbngHiB5xUprwGAAnwJ3DL39D2m43i_3YP-UO1TgZQUAOh7Jrd4foatpatTvBtY3F1DrCrUKE5Kkn770M",
            "e":"AQAB",
            "kid":"ZzAjSnraU3bkWGnnAqLapYGpTyNfLbjbzgAPbbW2GEA",
            "kty":"RSA",
            "n":"wWwQFtSzeRjjerpEM5Rmqz_DsNaZ9S1Bw6UbZkDLowuuTCjBWUax0vBMMxdy6XjEEK4Oq9lKMvx9JzjmeJf1knoqSNrox3Ka0rnxXpNAz6sATvme8p9mTXyp0cX4lF4U2J54xa2_S9NF5QWvpXvBeC4GAJx7QaSw4zrUkrc6XyaAiFnLhQEwKJCwUw4NOqIuYvYp_IXhw-5Ti_icDlZS-282PcccnBeOcX7vc21pozibIdmZJKqXNsL1Ibx5Nkx1F1jLnekJAmdaACDjYRLL_6n3W4wUp19UvzB1lGtXcJKLLkqB6YDiZNu16OSiSprfmrRXvYmvD8m6Fnl5aetgKw",
            "p":"7enorp9Pm9XSHaCvQyENcvdU99WCPbnp8vc0KnY_0g9UdX4ZDH07JwKu6DQEwfmUA1qspC-e_KFWTl3x0-I2eJRnHjLOoLrTjrVSBRhBMGEH5PvtZTTThnIY2LReH-6EhceGvcsJ_MhNDUEZLykiH1OnKhmRuvSdhi8oiETqtPE",
            "q":"0CBLGi_kRPLqI8yfVkpBbA9zkCAshgrWWn9hsq6a7Zl2LcLaLBRUxH0q1jWnXgeJh9o5v8sYGXwhbrmuypw7kJ0uA3OgEzSsNvX5Ay3R9sNel-3Mqm8Me5OfWWvmTEBOci8RwHstdR-7b9ZT13jk-dsZI7OlV_uBja1ny9Nz9ts",
            "qi":"pG6J4dcUDrDndMxa-ee1yG4KjZqqyCQcmPAfqklI2LmnpRIjcK78scclvpboI3JQyg6RCEKVMwAhVtQM6cBcIO3JrHgqeYDblp5wXHjto70HVW6Z8kBruNx1AH9E8LzNvSRL-JVTFzBkJuNgzKQfD0G77tQRgJ-Ri7qu3_9o1M4"
        }
        """;

    protected static string BuildPublicJwk(string privateJwk)
    {
        var fullKey = new JsonWebKey(privateJwk);
        var publicKey = new JsonWebKey
        {
            Kty = fullKey.Kty,
            N = fullKey.N,
            E = fullKey.E,
            Kid = fullKey.Kid,
            Alg = "RS256"
        };
        return JsonSerializer.Serialize(publicKey);
    }
}
