// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.AccessTokenManagement.AccessTokenHandler.Fixtures;
using Duende.AccessTokenManagement.AccessTokenHandler.Helpers;
using Duende.AccessTokenManagement.DPoP;
using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AccessTokenManagement.AccessTokenHandler;

public class AccessTokenHandlerTests(ITestOutputHelper output)
{
    public enum FixtureType
    {
        ClientCredentials,
        ClientCredentialsWithAutoTuning,
        OidcUser,
        OidcClient
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Can_get_access_token(FixtureType type)
    {
        var fixture = await GetInitializedFixture(type);

        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();

        fixture.ApiEndpoint.LastUsedAccessToken.ShouldBe("access_token_1");
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Access_tokens_are_cached(FixtureType type)
    {
        var fixture = await GetInitializedFixture(type);

        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();
        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();

        fixture.ApiEndpoint.LastUsedAccessToken.ShouldBe("access_token_1");
    }

    [Fact(Skip = "TODO: (pgermishuys) This test is flaky, needs investigation")]
    public async Task Uses_auto_tuning_in_cache_expiration()
    {
        // hybrid cache doesn't allow us to set the cache expiration based on the
        // lifetime of a token after it's retrieved. To circumvent this, we implemented cache auto-tuning.
        // Cache Auto tuning does the following:
        // the first time a token is retrieved, the cache expiration from the default setting is used
        // however, after that, it will remember the lifetime of the token, and use that to set the cache expiration

        var fixture = (ClientCredentialsFixtureWithAutoTuning)
            await GetInitializedFixture(FixtureType.ClientCredentialsWithAutoTuning);

        // We get an access token. The cache interval is not known, so we expect it to be cached for the default cache duration
        await EnsureTokenNumber(fixture, 1);

        // Ensure it's cached.
        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();
        fixture.ApiEndpoint.LastUsedAccessToken.ShouldBe("access_token_1");
        await EnsureTokenNumber(fixture, 1);

        // Increase the time by too little time
        AdvanceTimeBy(fixture, fixture.CacheExpiration - TimeSpan.FromSeconds(2));
        await EnsureTokenNumber(fixture, 1);

        // Increase the time by a bit more - now we expect the token to be expired, and a new one to be fetched
        AdvanceTimeBy(fixture, TimeSpan.FromSeconds(2));
        await EnsureTokenNumber(fixture, 2);

        // Now increase the time by the cache expiration again. It should NOT be expired now
        // because the auto-tuning has kicked in and has used the token expiration lifetime
        // (which is much longer)
        AdvanceTimeBy(fixture, fixture.CacheExpiration);
        await EnsureTokenNumber(fixture, 2);

        // But if we wait for the token expiration, then it should expire.
        AdvanceTimeBy(fixture, fixture.TokenExpiration);
        await EnsureTokenNumber(fixture, 3);
    }

    private static void AdvanceTimeBy(AccessTokenHandlingBaseFixture fixture, TimeSpan by)
        => fixture.The.TimeProvider.Advance(by);

    private static async Task EnsureTokenNumber(AccessTokenHandlingBaseFixture fixture, int number)
    {
        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();
        fixture.ApiEndpoint.LastUsedAccessToken.ShouldBe("access_token_" + number);
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Will_refresh_token_when_access_token_is_rejected(FixtureType type)
    {
        var fixture = await GetInitializedFixture(type);

        fixture.ApiEndpoint.RespondOnceWithUnauthorized();

        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();

        fixture.ApiEndpoint.LastUsedAccessToken.ShouldBe("access_token_2");
    }

    private class MyTokenRequestCustomizer(IHttpContextAccessor context) : ITokenRequestCustomizer
    {
        public async Task<TokenRequestParameters> Customize(HttpRequestContext httpRequest,
            TokenRequestParameters baseParameters,
            CancellationToken cancellationToken = default)
        {
            var idToken = await context.HttpContext!.GetTokenAsync("access_token");
            baseParameters.Parameters.Add("id_token_hint", idToken ?? "");
            return baseParameters;
        }
    }

    [Fact]
    public async Task Can_send_id_token()
    {
        var fixture = new OidcUserFixture();
        fixture.Services.AddSingleton<ITokenRequestCustomizer, MyTokenRequestCustomizer>();
        await fixture.InitializeAsync(output, null);

        fixture.ApiEndpoint.RespondOnceWithUnauthorized();

        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();

        fixture.ApiEndpoint.LastUsedAccessToken.ShouldBe("access_token_2");
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Will_only_retry_once(FixtureType type)
    {
        var fixture = await GetInitializedFixture(type);

        fixture.ApiEndpoint.RespondOnceWithUnauthorized();
        fixture.ApiEndpoint.RespondOnceWithUnauthorized();

        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode(HttpStatusCode.Unauthorized);

        fixture.ApiEndpoint.LastUsedAccessToken.ShouldBe("access_token_2");
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Will_use_DPop_on_api_requests(FixtureType type)
    {
        var fixture = await GetInitializedFixture(type, BuildDPoPProofKey());

        fixture.ApiEndpoint.ExpectCallWithoutNonce(replyWithNonce: "nonce_1");
        fixture.ApiEndpoint.ExpectCallWithNonce(expectedNonce: "nonce_1", replyWithNonce: "nonce_2");
        fixture.ApiEndpoint.ExpectCallWithNonce(expectedNonce: "nonce_2", replyWithNonce: "nonce_3");
        fixture.ApiEndpoint.ExpectCallWithNonce(expectedNonce: "nonce_3", replyWithNonce: "nonce_4");

        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();
        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();
        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();
    }

    [Theory]
    [InlineData("beareR", "Bearer")]
    [InlineData("dpoP", "DPoP")]
    public async Task Will_normalize_case_for_scheme_values(string tokenType, string expectedScheme)
    {
        var fixture = await GetInitializedFixture(FixtureType.ClientCredentials, BuildDPoPProofKey());
        fixture.TokenEndpoint.RespondWithTokenType(tokenType);
        fixture.ApiEndpoint.ExpectCallWithScheme(expectedScheme);

        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task ForceTokenRenewal_should_bypass_cache_and_get_new_token(FixtureType type)
    {
        var fixture = await GetInitializedFixture(type);

        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();
        fixture.ApiEndpoint.LastUsedAccessToken.ShouldBe("access_token_1");

        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();
        fixture.ApiEndpoint.LastUsedAccessToken.ShouldBe("access_token_1");

        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.SetForceRenewal(true);
        await fixture.HttpClient.SendAsync(request).CheckHttpStatusCode();
        fixture.ApiEndpoint.LastUsedAccessToken.ShouldBe("access_token_2");

        await fixture.HttpClient.GetAsync("/").CheckHttpStatusCode();
        fixture.ApiEndpoint.LastUsedAccessToken.ShouldBe("access_token_2");
    }

    private DPoPProofKey BuildDPoPProofKey(string alg = "ES256")
    {
        var key = CryptoHelper.CreateECDsaSecurityKey();
        var jwk = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(key);
        jwk.Alg = alg;
        return DPoPProofKey.Parse(JsonSerializer.Serialize(jwk));
    }

    private async Task<AccessTokenHandlingBaseFixture> GetInitializedFixture(FixtureType type, DPoPProofKey? dPoPJsonWebKey = null)
    {
        AccessTokenHandlingBaseFixture item = type switch
        {
            FixtureType.ClientCredentials => new ClientCredentialsFixture(),
            FixtureType.ClientCredentialsWithAutoTuning => new ClientCredentialsFixtureWithAutoTuning(),
            FixtureType.OidcClient => new OidcClientFixture(),
            FixtureType.OidcUser => new OidcUserFixture(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        await item.InitializeAsync(output, dPoPJsonWebKey);

        return item;
    }

    public static IEnumerable<object[]> AllFixtures() =>
        Enum.GetValues<FixtureType>()
            .Where(v => v != FixtureType.ClientCredentialsWithAutoTuning)
            .Select(value => (object[])[value]);
}
