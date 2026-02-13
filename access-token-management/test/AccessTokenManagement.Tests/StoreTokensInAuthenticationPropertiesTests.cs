//Copyright (c) Duende Software. All rights reserved.
//Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.Framework;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.AccessTokenManagement.OpenIdConnect.Internal;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.AccessTokenManagement;

public class StoreTokensInAuthenticationPropertiesTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private TestData The { get; } = new();
    public TestDataBuilder Some => new(The);

    [Fact]
    public async Task Should_be_able_to_store_and_retrieve_tokens()
    {
        var authenticationProperties = new AuthenticationProperties();
        var sut = new StoreTokensInAuthenticationProperties(
            new TestOptionsMonitor<UserTokenManagementOptions>(),
            new TestOptionsMonitor<CookieAuthenticationOptions>(),
            new TestSchemeProvider(),
            new NullLogger<StoreTokensInAuthenticationProperties>()
        );

        var userToken = GenerateRandomUserToken();

        await sut.SetUserTokenAsync(userToken, authenticationProperties, ct: _ct);
        var result = sut.GetUserToken(authenticationProperties);

        result.Succeeded.ShouldBeTrue();

        CompareUserToken(result, userToken);
        CompareRefreshToken(result, userToken);
    }

    private static void CompareRefreshToken(TokenResult<TokenForParameters> result, UserToken userToken)
    {
        var userRefreshToken = result.Token!.RefreshToken.ShouldNotBeNull();
        userRefreshToken.RefreshToken.ShouldBe(userToken.RefreshToken!.Value);
        userRefreshToken.DPoPProofKey.ShouldBe(userToken.DPoPJsonWebKey!.Value);
    }

    private static void CompareUserToken(TokenResult<TokenForParameters> result, UserToken userToken)
    {
        var tokenTokenForSpecifiedParameters = result.Token
            .ShouldNotBeNull()
            .TokenForSpecifiedParameters
            .ShouldNotBeNull();

        tokenTokenForSpecifiedParameters.AccessToken.ShouldBe(userToken.AccessToken);
        tokenTokenForSpecifiedParameters.Expiration.ShouldBe(userToken.Expiration);
        tokenTokenForSpecifiedParameters.DPoPJsonWebKey.ShouldBe(userToken.DPoPJsonWebKey);
        tokenTokenForSpecifiedParameters.AccessTokenType.ShouldBe(userToken.AccessTokenType);
        tokenTokenForSpecifiedParameters.ClientId.ShouldBe(userToken.ClientId);
        tokenTokenForSpecifiedParameters.IdentityToken.ShouldBe(userToken.IdentityToken);
        tokenTokenForSpecifiedParameters.RefreshToken.ShouldBe(userToken.RefreshToken);
    }

    [Fact]
    public async Task Should_be_able_to_store_and_retrieve_tokens_for_multiple_challenge_schemes()
    {
        var authenticationProperties = new AuthenticationProperties();
        var sut = new StoreTokensInAuthenticationProperties(
            new TestOptionsMonitor<UserTokenManagementOptions>(new UserTokenManagementOptions
            {
                UseChallengeSchemeScopedTokens = true
            }),
            new TestOptionsMonitor<CookieAuthenticationOptions>(),
            new TestSchemeProvider(),
            new NullLogger<StoreTokensInAuthenticationProperties>()
        );

        var tokenForScheme1 = GenerateRandomUserToken();
        var tokenForScheme2 = GenerateRandomUserToken();

        var scheme1RequestParameters = new UserTokenRequestParameters
        {
            ChallengeScheme = Scheme.Parse("scheme1")
        };
        var scheme2RequestParameters = new UserTokenRequestParameters
        {
            ChallengeScheme = Scheme.Parse("scheme2")
        };

        await sut.SetUserTokenAsync(tokenForScheme1, authenticationProperties, scheme1RequestParameters, _ct);
        await sut.SetUserTokenAsync(tokenForScheme2, authenticationProperties, scheme2RequestParameters, _ct);

        var resultScheme1 = sut.GetUserToken(authenticationProperties, scheme1RequestParameters);
        var resultScheme2 = sut.GetUserToken(authenticationProperties, scheme2RequestParameters);

        CompareUserToken(resultScheme1, tokenForScheme1);
        CompareRefreshToken(resultScheme1, tokenForScheme1);

        CompareUserToken(resultScheme2, tokenForScheme2);
        CompareRefreshToken(resultScheme2, tokenForScheme2);
    }

    [Fact]
    public async Task Should_be_able_to_store_and_retrieve_tokens_for_multiple_resources()
    {
        var authenticationProperties = new AuthenticationProperties();
        var sut = new StoreTokensInAuthenticationProperties(
            new TestOptionsMonitor<UserTokenManagementOptions>(),
            new TestOptionsMonitor<CookieAuthenticationOptions>(),
            new TestSchemeProvider(),
            new NullLogger<StoreTokensInAuthenticationProperties>()
        );

        var tokenForResource1 = GenerateRandomUserToken();
        var tokenForResource2 = GenerateAnotherTokenForADifferentResource(tokenForResource1);

        var resource1RequestParameters = new UserTokenRequestParameters
        {
            Resource = Resource.Parse("resource1"),
        };
        var resource2RequestParameters = new UserTokenRequestParameters
        {
            Resource = Resource.Parse("resource2"),
        };

        await sut.SetUserTokenAsync(tokenForResource1, authenticationProperties, resource1RequestParameters, _ct);
        await sut.SetUserTokenAsync(tokenForResource2, authenticationProperties, resource2RequestParameters, _ct);

        var resultForResource1 = sut.GetUserToken(authenticationProperties, resource1RequestParameters);
        var resultForResource2 = sut.GetUserToken(authenticationProperties, resource2RequestParameters);

        CompareUserToken(resultForResource1, tokenForResource1);
        CompareRefreshToken(resultForResource1, tokenForResource1);

        CompareUserToken(resultForResource2, tokenForResource2);
        CompareRefreshToken(resultForResource2, tokenForResource2);
    }

    [Fact]
    public async Task Should_be_able_to_store_and_retrieve_tokens_for_multiple_schemes_and_resources_at_the_same_time()
    {
        var authenticationProperties = new AuthenticationProperties();
        var sut = new StoreTokensInAuthenticationProperties(
            new TestOptionsMonitor<UserTokenManagementOptions>(new UserTokenManagementOptions
            {
                UseChallengeSchemeScopedTokens = true
            }),
            new TestOptionsMonitor<CookieAuthenticationOptions>(),
            new TestSchemeProvider(),
            new NullLogger<StoreTokensInAuthenticationProperties>()
        );

        var tokenForResource1Scheme1 = GenerateRandomUserToken();
        var tokenForResource1Scheme2 = GenerateRandomUserToken();
        var tokenForResource2Scheme1 = GenerateAnotherTokenForADifferentResource(tokenForResource1Scheme1);
        var tokenForResource2Scheme2 = GenerateAnotherTokenForADifferentResource(tokenForResource1Scheme2);

        var resource1Scheme1 = new UserTokenRequestParameters
        {
            Resource = Resource.Parse("resource1"),
            ChallengeScheme = Scheme.Parse("scheme1")
        };

        var resource1Scheme2 = new UserTokenRequestParameters
        {
            Resource = Resource.Parse("resource1"),
            ChallengeScheme = Scheme.Parse("scheme2")
        };

        var resource2Scheme1 = new UserTokenRequestParameters
        {
            Resource = Resource.Parse("resource2"),
            ChallengeScheme = Scheme.Parse("scheme1")
        };

        var resource2Scheme2 = new UserTokenRequestParameters
        {
            Resource = Resource.Parse("resource2"),
            ChallengeScheme = Scheme.Parse("scheme2")
        };

        await sut.SetUserTokenAsync(tokenForResource1Scheme1, authenticationProperties, resource1Scheme1, _ct);
        await sut.SetUserTokenAsync(tokenForResource1Scheme2, authenticationProperties, resource1Scheme2, _ct);
        await sut.SetUserTokenAsync(tokenForResource2Scheme1, authenticationProperties, resource2Scheme1, _ct);
        await sut.SetUserTokenAsync(tokenForResource2Scheme2, authenticationProperties, resource2Scheme2, _ct);

        var resultForResource1Scheme1 = sut.GetUserToken(authenticationProperties, resource1Scheme1);
        var resultForResource1Scheme2 = sut.GetUserToken(authenticationProperties, resource1Scheme2);
        var resultForResource2Scheme1 = sut.GetUserToken(authenticationProperties, resource2Scheme1);
        var resultForResource2Scheme2 = sut.GetUserToken(authenticationProperties, resource2Scheme2);

        CompareUserToken(resultForResource1Scheme1, tokenForResource1Scheme1);
        CompareRefreshToken(resultForResource1Scheme1, tokenForResource1Scheme1);

        CompareUserToken(resultForResource1Scheme2, tokenForResource1Scheme2);
        CompareRefreshToken(resultForResource1Scheme2, tokenForResource1Scheme2);

        CompareUserToken(resultForResource2Scheme1, tokenForResource2Scheme1);
        CompareRefreshToken(resultForResource2Scheme1, tokenForResource2Scheme1);

        CompareUserToken(resultForResource2Scheme2, tokenForResource2Scheme2);
        CompareRefreshToken(resultForResource2Scheme2, tokenForResource2Scheme2);
    }

    [Fact]
    public async Task Should_be_able_to_remove_tokens()
    {
        var authenticationProperties = new AuthenticationProperties();
        var sut = new StoreTokensInAuthenticationProperties(
            new TestOptionsMonitor<UserTokenManagementOptions>(),
            new TestOptionsMonitor<CookieAuthenticationOptions>(),
            new TestSchemeProvider(),
            new NullLogger<StoreTokensInAuthenticationProperties>()
        );

        var userToken = GenerateRandomUserToken();

        await sut.SetUserTokenAsync(userToken, authenticationProperties, ct: _ct);
        sut.RemoveUserToken(authenticationProperties);

        var result = sut.GetUserToken(authenticationProperties);

        result.Succeeded.ShouldBeFalse();

        result.Token?.RefreshToken.ShouldBeNull();
        result.Token?.TokenForSpecifiedParameters.ShouldBeNull();
    }


    [Fact]
    public async Task Should_be_able_to_remove_tokens_for_multiple_schemes_and_resources_at_the_same_time()
    {
        var authenticationProperties = new AuthenticationProperties();
        var sut = new StoreTokensInAuthenticationProperties(
            new TestOptionsMonitor<UserTokenManagementOptions>(new UserTokenManagementOptions
            {
                UseChallengeSchemeScopedTokens = true
            }),
            new TestOptionsMonitor<CookieAuthenticationOptions>(),
            new TestSchemeProvider(),
            new NullLogger<StoreTokensInAuthenticationProperties>()
        );

        var tokenForResource1Scheme1 = GenerateRandomUserToken();
        var tokenForResource1Scheme2 = GenerateRandomUserToken();
        var tokenForResource2Scheme1 = GenerateAnotherTokenForADifferentResource(tokenForResource1Scheme1);
        var tokenForResource2Scheme2 = GenerateAnotherTokenForADifferentResource(tokenForResource1Scheme2);

        var resource1Scheme1 = new UserTokenRequestParameters
        {
            Resource = Resource.Parse("resource1"),
            ChallengeScheme = Scheme.Parse("scheme1")
        };

        var resource1Scheme2 = new UserTokenRequestParameters
        {
            Resource = Resource.Parse("resource1"),
            ChallengeScheme = Scheme.Parse("scheme2")
        };

        var resource2Scheme1 = new UserTokenRequestParameters
        {
            Resource = Resource.Parse("resource2"),
            ChallengeScheme = Scheme.Parse("scheme1")
        };

        var resource2Scheme2 = new UserTokenRequestParameters
        {
            Resource = Resource.Parse("resource2"),
            ChallengeScheme = Scheme.Parse("scheme2")
        };

        await sut.SetUserTokenAsync(tokenForResource1Scheme1, authenticationProperties, resource1Scheme1, _ct);
        await sut.SetUserTokenAsync(tokenForResource1Scheme2, authenticationProperties, resource1Scheme2, _ct);
        await sut.SetUserTokenAsync(tokenForResource2Scheme1, authenticationProperties, resource2Scheme1, _ct);
        await sut.SetUserTokenAsync(tokenForResource2Scheme2, authenticationProperties, resource2Scheme2, _ct);

        sut.RemoveUserToken(authenticationProperties, resource1Scheme1);
        sut.RemoveUserToken(authenticationProperties, resource2Scheme2);

        var resultForResource1Scheme1 = sut.GetUserToken(authenticationProperties, resource1Scheme1);
        var resultForResource1Scheme2 = sut.GetUserToken(authenticationProperties, resource1Scheme2);
        var resultForResource2Scheme1 = sut.GetUserToken(authenticationProperties, resource2Scheme1);
        var resultForResource2Scheme2 = sut.GetUserToken(authenticationProperties, resource2Scheme2);

        resultForResource1Scheme1.Token?.TokenForSpecifiedParameters.ShouldBeNull();
        resultForResource2Scheme2.Token?.TokenForSpecifiedParameters.ShouldBeNull();

        CompareUserToken(resultForResource2Scheme1, tokenForResource2Scheme1);
        CompareRefreshToken(resultForResource2Scheme1, tokenForResource2Scheme1);
        CompareUserToken(resultForResource2Scheme1, tokenForResource2Scheme1);
        CompareRefreshToken(resultForResource2Scheme1, tokenForResource2Scheme1);
    }


    [Fact]
    public async Task Removing_all_tokens_in_a_challenge_scheme_should_remove_items_shared_in_that_scheme()
    {
        var authenticationProperties = new AuthenticationProperties();
        var sut = new StoreTokensInAuthenticationProperties(
            new TestOptionsMonitor<UserTokenManagementOptions>(new UserTokenManagementOptions
            {
                UseChallengeSchemeScopedTokens = true
            }),
            new TestOptionsMonitor<CookieAuthenticationOptions>(),
            new TestSchemeProvider(),
            new NullLogger<StoreTokensInAuthenticationProperties>()
        );

        var tokenForResource1Scheme1 = GenerateRandomUserToken();
        var tokenForResource1Scheme2 = GenerateRandomUserToken();
        var tokenForResource2Scheme1 = GenerateAnotherTokenForADifferentResource(tokenForResource1Scheme1);
        var tokenForResource2Scheme2 = GenerateAnotherTokenForADifferentResource(tokenForResource1Scheme2);

        var resource1Scheme1 = new UserTokenRequestParameters
        {
            Resource = Resource.Parse("resource1"),
            ChallengeScheme = Scheme.Parse("scheme1")
        };

        var resource1Scheme2 = new UserTokenRequestParameters
        {
            Resource = Resource.Parse("resource1"),
            ChallengeScheme = Scheme.Parse("scheme2")
        };

        var resource2Scheme1 = new UserTokenRequestParameters
        {
            Resource = Resource.Parse("resource2"),
            ChallengeScheme = Scheme.Parse("scheme1")
        };

        var resource2Scheme2 = new UserTokenRequestParameters
        {
            Resource = Resource.Parse("resource2"),
            ChallengeScheme = Scheme.Parse("scheme2")
        };

        await sut.SetUserTokenAsync(tokenForResource1Scheme1, authenticationProperties, resource1Scheme1, _ct);
        await sut.SetUserTokenAsync(tokenForResource1Scheme2, authenticationProperties, resource1Scheme2, _ct);
        await sut.SetUserTokenAsync(tokenForResource2Scheme1, authenticationProperties, resource2Scheme1, _ct);
        await sut.SetUserTokenAsync(tokenForResource2Scheme2, authenticationProperties, resource2Scheme2, _ct);

        sut.RemoveUserToken(authenticationProperties, resource1Scheme1);
        sut.RemoveUserToken(authenticationProperties, resource1Scheme2);
        sut.RemoveUserToken(authenticationProperties, resource2Scheme1);
        sut.RemoveUserToken(authenticationProperties, resource2Scheme2);

        var resultForResource1Scheme1 = sut.GetUserToken(authenticationProperties, resource1Scheme1);
        resultForResource1Scheme1.Token?.RefreshToken.ShouldBeNull();
        resultForResource1Scheme1.Token?.TokenForSpecifiedParameters?.DPoPJsonWebKey.ShouldBeNull();
    }

    private Random r = new(DateTimeOffset.Now.Millisecond);

    private UserToken GenerateRandomUserToken() => new()
    {
        AccessToken = AccessToken.Parse(Guid.NewGuid().ToString()),
        AccessTokenType = AccessTokenType.Parse(r.NextInt64().ToString()),
        RefreshToken = RefreshToken.Parse(Guid.NewGuid().ToString()),
        ClientId = ClientId.Parse("some_client-id"),
        IdentityToken = IdentityToken.Parse(Guid.NewGuid().ToString()),
        Scope = Scope.Parse("some_scope"),
        Expiration = new DateTimeOffset(new DateTime(DateTime.Now.Ticks + Random.Shared.Next())),
        DPoPJsonWebKey = The.JsonWebKey,
    };

    private UserToken GenerateAnotherTokenForADifferentResource(UserToken previousToken) => new()
    {
        AccessToken = AccessToken.Parse(Guid.NewGuid().ToString()),
        AccessTokenType = AccessTokenType.Parse(r.NextInt64().ToString()),
        Expiration = new DateTimeOffset(new DateTime(DateTime.Now.Ticks + Random.Shared.Next())),

        // These two values don't change when we switch resources
        RefreshToken = previousToken.RefreshToken,
        DPoPJsonWebKey = previousToken.DPoPJsonWebKey,
        ClientId = ClientId.Parse("some_client-id"),
        IdentityToken = IdentityToken.Parse(Guid.NewGuid().ToString()),
        Scope = Scope.Parse("some_scope"),

    };
}
