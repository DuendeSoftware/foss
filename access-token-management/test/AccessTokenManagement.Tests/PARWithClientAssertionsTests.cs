// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net.Http.Json;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.Framework;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using JsonWebKey = Microsoft.IdentityModel.Tokens.JsonWebKey;

namespace Duende.AccessTokenManagement;

#if NET9_0_OR_GREATER
/// <summary>
/// Tests that client assertions are sent on Pushed Authorization Requests (PAR).
/// </summary>
public sealed class PARWithClientAssertionsTests : IntegrationTestBase
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public PARWithClientAssertionsTests(ITestOutputHelper output)
        : base(output, "par-assertion")
    {
        IdentityServerHost.EnablePar = true;
        AppHost.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Require;
        AppHost.ClientAssertionSigningCredentials =
            new SigningCredentials(new JsonWebKey(ClientAssertionPrivateJwk), "RS256");
    }

    [Fact]
    public async Task LoginWithPARAndClientAssertionsShouldSucceed()
    {
        await InitializeAsync();

        await AppHost.LoginAsync("alice");

        // Verify PAR request contained client assertion
        var parRequest = IdentityServerHost.CapturedParRequests
            .FirstOrDefault();
        parRequest.ShouldNotBeNull("Expected a PAR request to be captured");
        parRequest.ShouldContainKeyAndValue(OidcConstants.TokenRequest.ClientAssertionType,
            OidcConstants.ClientAssertionTypes.JwtBearer);
        parRequest.ShouldContainKey(OidcConstants.TokenRequest.ClientAssertion);

        // Verify token exchange also used client assertion
        var codeExchangeRequest = IdentityServerHost.CapturedTokenRequests
            .FirstOrDefault(r => r.TryGetValue("grant_type", out var gt) && gt == "authorization_code");
        codeExchangeRequest.ShouldNotBeNull();
        codeExchangeRequest.ShouldContainKeyAndValue(OidcConstants.TokenRequest.ClientAssertionType,
            OidcConstants.ClientAssertionTypes.JwtBearer);
        codeExchangeRequest.ShouldContainKey(OidcConstants.TokenRequest.ClientAssertion);
    }

    [Fact]
    public async Task RefreshWithPARAndClientAssertionsShouldSucceed()
    {
        await InitializeAsync();
        await AppHost.LoginAsync("alice");

        // This forces a refresh (token lifetime is 10s, within RefreshBeforeExpiration window)
        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/user_token"), _ct);
        var token = await response.Content.ReadFromJsonAsync<UserTokenModel>(_ct);
        token.ShouldNotBeNull();

        // Verify refresh token request used client assertions
        var refreshRequests = IdentityServerHost.CapturedTokenRequests
            .Where(r => r.TryGetValue("grant_type", out var gt) && gt == "refresh_token")
            .ToList();
        refreshRequests.ShouldNotBeEmpty();
        foreach (var req in refreshRequests)
        {
            req.ShouldContainKeyAndValue(OidcConstants.TokenRequest.ClientAssertionType,
                OidcConstants.ClientAssertionTypes.JwtBearer);
            req.ShouldContainKey(OidcConstants.TokenRequest.ClientAssertion);
        }
    }
}

/// <summary>
/// Tests that both client assertions AND DPoP thumbprint are sent on PAR requests.
/// </summary>
public sealed class PARWithDPoPAndClientAssertionsTests : IntegrationTestBase
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public PARWithDPoPAndClientAssertionsTests(ITestOutputHelper output)
        : base(output, "par-dpop-assertion",
            configureUserTokenManagementOptions: opt => { opt.DPoPJsonWebKey = DPoPProofKey.Parse(DPoPPrivateJwk); })
    {
        IdentityServerHost.EnablePar = true;
        AppHost.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Require;
        AppHost.ClientAssertionSigningCredentials =
            new SigningCredentials(new JsonWebKey(ClientAssertionPrivateJwk), "RS256");
    }

    [Fact]
    public async Task LoginWithPARDPoPAndClientAssertionsShouldSucceed()
    {
        await InitializeAsync();

        await AppHost.LoginAsync("alice");

        // Verify PAR request contained client assertion AND DPoP thumbprint
        var parRequest = IdentityServerHost.CapturedParRequests
            .FirstOrDefault();
        parRequest.ShouldNotBeNull("Expected a PAR request to be captured");
        parRequest.ShouldContainKeyAndValue(OidcConstants.TokenRequest.ClientAssertionType,
            OidcConstants.ClientAssertionTypes.JwtBearer);
        parRequest.ShouldContainKey(OidcConstants.TokenRequest.ClientAssertion);
        parRequest.ShouldContainKey(OidcConstants.AuthorizeRequest.DPoPKeyThumbprint);

        // Verify we got a DPoP token back
        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/user_token"), _ct);
        var token = await response.Content.ReadFromJsonAsync<UserTokenModel>(_ct);
        token.ShouldNotBeNull();
        token.AccessTokenType.ShouldBe("DPoP");
    }

    [Fact]
    public async Task RefreshWithPARDPoPAndClientAssertionsShouldSucceed()
    {
        // Use always-null nonce store so the server always issues a nonce challenge,
        // guaranteeing the DPoP nonce retry deterministically (no wall-clock dependency).
        AppHost.OnConfigureServices += services =>
            services.AddSingleton<IDPoPNonceStore>(new TestDPoPNonceStore());

        await InitializeAsync();
        await AppHost.LoginAsync("alice");

        // This forces a refresh (token lifetime is 10s, within RefreshBeforeExpiration window)
        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/user_token"), _ct);
        var token = await response.Content.ReadFromJsonAsync<UserTokenModel>(_ct);
        token.ShouldNotBeNull();
        token.AccessTokenType.ShouldBe("DPoP");

        // Verify refresh token requests used client assertions
        var refreshRequests = IdentityServerHost.CapturedTokenRequests
            .Where(r => r.TryGetValue("grant_type", out var gt) && gt == "refresh_token")
            .ToList();
        refreshRequests.ShouldNotBeEmpty();
        foreach (var req in refreshRequests)
        {
            req.ShouldContainKeyAndValue(OidcConstants.TokenRequest.ClientAssertionType,
                OidcConstants.ClientAssertionTypes.JwtBearer);
            req.ShouldContainKey(OidcConstants.TokenRequest.ClientAssertion);
        }

        // Nonce retry must have occurred
        refreshRequests.Count.ShouldBe(2, "Expected exactly 2 refresh requests (initial + DPoP nonce retry)");

        var assertions = refreshRequests
            .Select(r => r[OidcConstants.TokenRequest.ClientAssertion])
            .ToList();
        assertions.Distinct().Count().ShouldBe(assertions.Count,
            "Client assertions must be unique across DPoP nonce retries during refresh");
    }

    private const string DPoPPrivateJwk =
        """
        {
            "kty":"RSA",
            "n":"0vx7agoebGcQSuuPiLJXZptN9nndrQmbXEps2aiAFbWhM78LhWx4cbbfAAtVT86zwu1RK7aPFFxuhDR1L6tSoc_BJECPebWKRXjBZCiFV4n3oknjhMstn64tZ_2W-5JsGY4Hc5n9yBXArwl93lqt7_RN5w6Cf0h4QyQ5v-65YGjQR0_FDW2QvzqY368QQMicAtaSqzs8KJZgnYb9c7d0zgdAZHzu6qMQvRL5hajrn1n91CbOpbISD08qNLyrdkt-bFTWhAI4vMQFh6WeZu0fM4lFd2NcRwr3XPksINHaQ-G_xBniIqbw0Ls1jF44-csFCur-kEgU8awapJzKnqDKgw",
            "e":"AQAB",
            "d":"X4cTteJY_gn4FYPsXB8rdXix5vwsg1FLN5E3EaG6RJoVH-HLLKD9M7dx5oo7GURknchnrRweUkC7hT5fJLM0WbFAKNLWY2vv7B6NqXSzUvxT0_YSfqijwp3RTzlBaCxWp4doFk5N2o8Gy_nHNKroADIkJ46pRUohsXywbReAdYaMwFs9tv8d_cPVY3i07a3t8MN6TNwm0dSawm9v47UiCl3Sk5ZiG7xojPLu4sbg1U2jx4IBTNBznbJSzFHK66jT8bgkuqsk0GjskDJk19Z4qwjwbsnn4j2WBii3RL-Us2lGVkY8fkFzme1z0HbIkfz0Y6mqnOYtqc0X4jfcKoAC8Q",
            "p":"83i-7IvMGXoMXCskv73TKr8637FiO7Z27zv8oj6pbWUQyLPQBQxtPVnwD20R-60eTDmD2ujnMt5PoqMrm8RfmNhVWDtjjMmCMjOpSXicFHj7XOuVIYQyqVWlWEh6dN36GVZYk93N8Bc9vY41xy8B9RzzOGVQzXvNEvn7O0nVbfs",
            "q":"3dfOR9cuYq-0S-mkFLzgItgMEfFzB2q3hWehMuG0oCuqnb3vobLyumqjVZQO1dIrdwgTnCdpYzBcOfW5r370AFXjiWft_NGEiovonizhKpo9VVS78TzFgxkIdrecRezsZ-1kYd_s1qDbxtkDEgfAITAG9LUnADun4vIcb6yelxk",
            "dp":"G4sPXkc6Ya9y8oJW9_ILj4xuppu0lzi_H7VTkS8xj5SdX3coE0oimYwxIi2emTAue0UOa5dpgFGyBJ4c8tQ2VF402XRugKDTP8akYhFo5tAA77Qe_NmtuYZc3C3m3I24G2GvR5sSDxUyAN2zq8Lfn9EUms6rY3Ob8YeiKkTiBj0",
            "dq":"s9lAH9fggBsoFR8Oac2R_E2gw282rT2kGOAhvIllETE1efrA6huUUvMfBcMpn8lqeW6vzznYY5SSQF7pMdC_agI3nG8Ibp1BUb0JUiraRNqUfLhcQb_d9GF4Dh7e74WbRsobRonujTYN1xCaP6TO61jvWrX-L18txXw494Q_cgk",
            "qi":"GyM_p6JrXySiz1toFgKbWV-JdI3jQ4ypu9rbMWx3rQJBfmt0FoYzgUIZEVFEcOqwemRN81zoDAaa-Bk0KWNGDjJHZDdDmFhW3AN7lI-puxk_mHZGJ11rxyR8O55XLSe3SPmRfKwZI6yU24ZxvQKFYItdldUKGzO6Ia6zTKhAVRU",
            "alg":"RS256",
            "kid":"2011-04-29"
        }
        """;
}

#endif
