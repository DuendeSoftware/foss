// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

#if NET8_0_OR_GREATER

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient.DPoP.Framework;
using Duende.IdentityServer.Models;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityModel.OidcClient.DPoP;

public class DPoPTest : IntegrationTestBase
{

    private static readonly string _jwkJson;
    private readonly IdentityServer.Models.Client _client;

    static DPoPTest()
    {
        var key = IdentityServer.Configuration.CryptoHelper.CreateRsaSecurityKey();
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        jwk.Alg = "RS256";
        _jwkJson = JsonSerializer.Serialize(jwk);
    }

    public DPoPTest()
    {
        IdentityServerHost.ApiScopes.Add(new ApiScope("scope1"));

        IdentityServerHost.Clients.Add(_client = new IdentityServer.Models.Client
        {
            ClientId = "client_credentials_client",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = { "scope1" },
            RequireDPoP = true,
        });
    }

    [Fact]
    public async Task dpop_tokens_should_be_passed_to_token_endpoint()
    {
        var handler = new ProofTokenMessageHandler(_jwkJson, IdentityServerHost.Server.CreateHandler());
        var client = new HttpClient(handler);

        var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = IdentityServerHost.Url("/connect/token"),
            ClientId = "client_credentials_client",
            ClientSecret = "secret",
        });

        tokenResponse.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        tokenResponse.TokenType.ShouldBe("DPoP");
    }

    [Fact]
    public async Task when_nonce_required_nonce_should_be_used_for_token_endpoint()
    {
        _client.DPoPValidationMode = DPoPTokenExpirationValidationMode.Nonce;

        var handler = new ProofTokenMessageHandler(_jwkJson, IdentityServerHost.Server.CreateHandler());
        var client = new HttpClient(handler);

        var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = IdentityServerHost.Url("/connect/token"),
            ClientId = "client_credentials_client",
            ClientSecret = "secret",
        });

        tokenResponse.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        tokenResponse.TokenType.ShouldBe("DPoP");
    }

    [Fact]
    public async Task dpop_tokens_should_be_passed_to_api()
    {
        var tokenHandler = new ProofTokenMessageHandler(_jwkJson, IdentityServerHost.Server.CreateHandler());
        var tokenClient = new HttpClient(tokenHandler);

        var tokenResponse = await tokenClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = IdentityServerHost.Url("/connect/token"),
            ClientId = "client_credentials_client",
            ClientSecret = "secret",
        });

        var apiHandler = new ProofTokenMessageHandler(_jwkJson, ApiHost.Server.CreateHandler());
        var apiClient = new HttpClient(apiHandler);
        apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("DPoP", tokenResponse.AccessToken);

        ApiHost.ApiInvoked += ctx =>
        {
            ctx.User.Identity.IsAuthenticated.ShouldBeTrue();
        };

        var apiResponse = await apiClient.GetAsync(ApiHost.Url("/api"));
        apiResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task when_nonce_required_nonce_should_be_used_for_api_endpoint()
    {
        ApiHost = new ApiHost(IdentityServerHost);
        ApiHost.ValidateNonce = true;
        await ApiHost.InitializeAsync();

        var tokenHandler = new ProofTokenMessageHandler(_jwkJson, IdentityServerHost.Server.CreateHandler());
        var tokenClient = new HttpClient(tokenHandler);

        var tokenResponse = await tokenClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = IdentityServerHost.Url("/connect/token"),
            ClientId = "client_credentials_client",
            ClientSecret = "secret",
        });

        var apiHandler = new ProofTokenMessageHandler(_jwkJson, ApiHost.Server.CreateHandler());
        var apiClient = new HttpClient(apiHandler);
        apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("DPoP", tokenResponse.AccessToken);

        ApiHost.ApiInvoked += ctx =>
        {
            ctx.User.Identity.IsAuthenticated.ShouldBeTrue();
        };

        var apiResponse = await apiClient.GetAsync(ApiHost.Url("/api"));
        apiResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

#endif
