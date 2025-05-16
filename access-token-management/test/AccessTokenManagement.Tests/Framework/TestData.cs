// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Duende.AccessTokenManagement.DPoP;
using Duende.IdentityServer.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AccessTokenManagement.Framework;

public class TestData
{
    public AccessTokenString AccessToken { get; set; } = AccessTokenString.Parse(SameNameAsProperty());
    public AccessTokenType TokenType { get; set; } = AccessTokenType.Parse("tokentype");
    public Scope Scope { get; set; } = Scope.Parse("scope");

    public ClientId ClientId { get; set; } = SameNameAsProperty();
    public ClientSecret ClientSecret { get; set; } = SameNameAsProperty();
    public Uri Authority { get; set; } = new Uri("https://authority");
    public Uri TokenEndpoint { get; set; } = new Uri("https://authority/connect/token");
    public ProofKeyString JsonWebKey { get; set; } = BuildDPoPJsonWebKey();
    public DateTimeOffset CurrentDate { get; set; } = new DateTimeOffset(2000, 1, 2, 3, 4, 5, TimeSpan.FromHours(6));
    public int ExpiresInSeconds { get; set; } = 60;
    public Resource Resource { get; set; } = SameNameAsProperty();

    public static ProofKeyString BuildDPoPJsonWebKey()
    {
        var key = CryptoHelper.CreateRsaSecurityKey();
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        jwk.Alg = "RS256";
        var jwkJson = JsonSerializer.Serialize(jwk);
        return jwkJson;
    }

    private static string SameNameAsProperty([CallerMemberName] string? name = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        return Regex.Replace(name, "(?<!^)([A-Z])", "_$1").ToLower();
    }
}
