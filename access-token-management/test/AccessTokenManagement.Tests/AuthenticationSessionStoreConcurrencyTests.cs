// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.AccessTokenManagement.AccessTokenHandler.Helpers;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Refreshes for different resources can store their tokens concurrently within one HTTP request.
/// The default store must not corrupt or lose either of them.
/// </summary>
public class AuthenticationSessionStoreConcurrencyTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Concurrent_stores_for_different_resources_in_one_request_should_keep_both_tokens()
    {
        var authentication = new SharedSessionAuthenticationService();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication(o => o.DefaultSignInScheme = "cookie").AddCookie("cookie");
        services.AddSingleton<IAuthenticationService>(authentication);
        services.AddOpenIdConnectAccessTokenManagement();
        var accessor = new FakeHttpContextAccessor();
        services.AddSingleton<IHttpContextAccessor>(accessor);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        accessor.HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider, User = authentication.Principal };
        var store = scope.ServiceProvider.GetRequiredService<IUserTokenStore>();

        await Task.WhenAll(
            Task.Run(() => store.StoreTokenAsync(authentication.Principal, Token("at_res1"), ForResource("urn:res1"), _ct), _ct),
            Task.Run(() => store.StoreTokenAsync(authentication.Principal, Token("at_res2"), ForResource("urn:res2"), _ct), _ct));

        authentication.MaxConcurrentSignIns.ShouldBe(1);

        var tokensInProps = provider.GetRequiredService<IStoreTokensInAuthenticationProperties>();
        var signedIn = authentication.LastSignedInProperties.ShouldNotBeNull();
        tokensInProps.GetUserToken(signedIn, ForResource("urn:res1")).Token!.TokenForSpecifiedParameters!.AccessToken.ToString().ShouldBe("at_res1");
        tokensInProps.GetUserToken(signedIn, ForResource("urn:res2")).Token!.TokenForSpecifiedParameters!.AccessToken.ToString().ShouldBe("at_res2");
    }

    private static UserTokenRequestParameters ForResource(string resource) =>
        new() { Resource = Resource.Parse(resource) };

    private static UserToken Token(string accessToken) => new()
    {
        AccessToken = AccessToken.Parse(accessToken),
        AccessTokenType = AccessTokenType.Parse("Bearer"),
        ClientId = ClientId.Parse("client"),
        Expiration = DateTimeOffset.UtcNow.AddHours(1),
        RefreshToken = RefreshToken.Parse("shared_refresh_token"),
    };

    /// <summary>
    /// Like the cookie handler within one request: every authenticate returns the same session (one
    /// properties object). Sign-in takes a moment, so overlapping sign-ins would be seen.
    /// </summary>
    private class SharedSessionAuthenticationService : IAuthenticationService
    {
        private readonly AuthenticationProperties _session = new();
        private int _concurrentSignIns;
        private int _maxConcurrentSignIns;

        public ClaimsPrincipal Principal { get; } = new(new ClaimsIdentity([new Claim("sub", "alice")], "test"));
        public int MaxConcurrentSignIns => _maxConcurrentSignIns;
        public AuthenticationProperties? LastSignedInProperties { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(Principal, _session, "cookie")));

        public async Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            var current = Interlocked.Increment(ref _concurrentSignIns);
            InterlockedMax(ref _maxConcurrentSignIns, current);
            await Task.Delay(200);
            LastSignedInProperties = new AuthenticationProperties(new Dictionary<string, string?>(properties!.Items));
            Interlocked.Decrement(ref _concurrentSignIns);
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        private static void InterlockedMax(ref int target, int value)
        {
            int current;
            while (value > (current = Volatile.Read(ref target)) && Interlocked.CompareExchange(ref target, value, current) != current)
            {
            }
        }
    }
}
