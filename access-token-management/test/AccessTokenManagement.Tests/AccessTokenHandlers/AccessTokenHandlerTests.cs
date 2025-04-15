// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.AccessTokenManagement.AccessTokenHandlers.Fixtures;
using Duende.IdentityServer.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AccessTokenManagement;

public class AccessTokenHandlerTests(ITestOutputHelper output)
{
    public enum FixtureType
    {
        ClientCredentials,
        OidcUser,
        OidcClient
    }


    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Can_get_access_token(FixtureType type, bool usePreview)
    {
        var fixture = await GetInitializedFixture(type, usePreview);


        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();

        fixture.ApiEndpoint.LastUsedAccessToken.ShouldBe("access_token_1");
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Access_tokens_are_cached(FixtureType type, bool usePreview)
    {
        var fixture = await GetInitializedFixture(type, usePreview);


        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();
        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();

        fixture.ApiEndpoint.LastUsedAccessToken.ShouldBe("access_token_1");
    }
    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Will_refresh_token_when_access_token_is_rejected(FixtureType type, bool usePreview)
    {
        var fixture = await GetInitializedFixture(type, usePreview);

        fixture.ApiEndpoint.RespondOnceWithUnauthorized();

        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();

        fixture.ApiEndpoint.LastUsedAccessToken.ShouldBe("access_token_2");
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Will_only_retry_once(FixtureType type, bool usePreview)
    {
        var fixture = await GetInitializedFixture(type, usePreview);


        fixture.ApiEndpoint.RespondOnceWithUnauthorized();
        fixture.ApiEndpoint.RespondOnceWithUnauthorized();

        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode(HttpStatusCode.Unauthorized);

        fixture.ApiEndpoint.LastUsedAccessToken.ShouldBe("access_token_2");
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Will_use_DPop_on_api_requests(FixtureType type, bool usePreview)
    {
        var fixture = await GetInitializedFixture(type, usePreview, BuildDPoPProofKey());

        fixture.ApiEndpoint.ExpectCallWithoutNonce(replyWithNonce: "nonce_1");
        fixture.ApiEndpoint.ExpectCallWithNonce(expectedNonce: "nonce_1", replyWithNonce: "nonce_2");
        fixture.ApiEndpoint.ExpectCallWithNonce(expectedNonce: "nonce_2", replyWithNonce: "nonce_3");
        fixture.ApiEndpoint.ExpectCallWithNonce(expectedNonce: "nonce_3", replyWithNonce: "nonce_4");

        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();
        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();
        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();

    }

    [Theory]
    [InlineData("beareR", "Bearer", true)]
    [InlineData("beareR", "Bearer", false)]
    [InlineData("dpoP", "DPoP", true)]
    [InlineData("dpoP", "DPoP", false)]
    public async Task Will_normalize_case_for_scheme_values(string tokenType, string expectedScheme, bool usePreview)
    {
        var fixture = await GetInitializedFixture(FixtureType.ClientCredentials, usePreview, BuildDPoPProofKey());
        fixture.TokenEndpoint.RespondWithTokenType(tokenType);
        fixture.ApiEndpoint.ExpectCallWithScheme(expectedScheme);

        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();
    }

    private string BuildDPoPProofKey(string alg = "ES256")
    {
        var key = CryptoHelper.CreateECDsaSecurityKey();
        var jwk = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(key);
        jwk.Alg = alg;
        return JsonSerializer.Serialize(jwk);
    }

    private async Task<AccessTokenHandlingBaseFixture> GetInitializedFixture(FixtureType type, bool usePreview, string? dPoPJsonWebKey = null)
    {
        AccessTokenHandlingBaseFixture item = type switch
        {
            FixtureType.ClientCredentials => new ClientCredentialsFixture(),
            FixtureType.OidcClient => new OidcClientFixture(),
            FixtureType.OidcUser => new OidcUserFixture(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        await item.InitializeAsync(output, usePreview, dPoPJsonWebKey);

        return item;
    }


    public static IEnumerable<object[]> AllFixtures()
    {
        foreach (var value in Enum.GetValues<FixtureType>())
        {
            yield return [value, true];
            yield return [value, false];
        }
    }

}
