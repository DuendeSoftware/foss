// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.AccessTokenManagement.OpenIdConnect.Internal;
using Duende.AccessTokenManagement.OTel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Concurrent user token requests that share a refresh token.
/// See https://github.com/DuendeSoftware/foss/issues/360
/// </summary>
public class UserTokenRequestConcurrencyTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly ClaimsPrincipal _user = new(new ClaimsIdentity([new Claim("sub", "alice")], "test"));
    private readonly BlockingTokenEndpoint _tokenEndpoint = new();
    private readonly CountingConcurrencyControl _control = new(new UserTokenRequestConcurrencyControl());

    /// <summary>
    /// The real control, counting how many calls have reached it.
    /// </summary>
    private class CountingConcurrencyControl(IUserTokenRequestConcurrencyControl inner) : IUserTokenRequestConcurrencyControl
    {
        private int _entered;
        public int Entered => _entered;

        public Task<TokenResult<UserToken>> ExecuteWithConcurrencyControlAsync(UserRefreshToken key, Func<Task<TokenResult<UserToken>>> tokenRetriever, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _entered);
            return inner.ExecuteWithConcurrencyControlAsync(key, tokenRetriever, ct);
        }

        public Task<TokenResult<UserToken>> ExecuteWithConcurrencyControlAsync(UserRefreshToken key, UserTokenRequestParameters parameters, Func<Task<TokenResult<UserToken>>> tokenRetriever, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _entered);
            return inner.ExecuteWithConcurrencyControlAsync(key, parameters, tokenRetriever, ct);
        }
    }

    [Fact]
    public async Task Concurrent_refreshes_for_different_resources_should_each_get_their_own_token()
    {
        var sut = CreateSut();

        // First call starts a refresh for res1 and is held inside the token endpoint.
        var res1Call = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);
        await _tokenEndpoint.FirstRequestStarted.WaitAsync(_ct);

        // Second call, same user / same refresh token, but a different resource.
        var res2Call = sut.GetAccessTokenAsync(_user, ForResource("urn:res2"), _ct);
        await WaitUntilAsync(() => _tokenEndpoint.Calls == 2); // both are at the token endpoint at the same time

        _tokenEndpoint.Release();

        (await res1Call).Token!.AccessToken.ToString().ShouldStartWith("at_for_urn:res1");
        (await res2Call).Token!.AccessToken.ToString().ShouldStartWith("at_for_urn:res2");
    }

    [Fact]
    public async Task Concurrent_refreshes_for_the_same_parameters_should_share_one_token_request()
    {
        var sut = CreateSut();

        var first = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);
        await _tokenEndpoint.FirstRequestStarted.WaitAsync(_ct);
        var second = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);
        await WaitUntilAsync(() => _control.Entered == 2); // the second call has joined while the first is held

        _tokenEndpoint.Release();

        var firstResult = await first;
        var secondResult = await second;

        _tokenEndpoint.Calls.ShouldBe(1);
        secondResult.Token!.AccessToken.ShouldBe(firstResult.Token!.AccessToken);
    }

    [Fact]
    public async Task Concurrent_refreshes_with_different_client_assertions_should_not_share_a_token()
    {
        var sut = CreateSut();
        var withAssertion = (string value) => new UserTokenRequestParameters
        {
            Resource = Resource.Parse("urn:res1"),
            Assertion = new Duende.IdentityModel.Client.ClientAssertion { Type = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer", Value = value },
        };

        var first = sut.GetAccessTokenAsync(_user, withAssertion("assertion-1"), _ct);
        await _tokenEndpoint.FirstRequestStarted.WaitAsync(_ct);
        var second = sut.GetAccessTokenAsync(_user, withAssertion("assertion-2"), _ct);
        await WaitUntilAsync(() => _tokenEndpoint.Calls == 2);

        _tokenEndpoint.Release();

        (await second).Token!.AccessToken.ShouldNotBe((await first).Token!.AccessToken);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 500 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        condition().ShouldBeTrue();
    }

    [Fact]
    public async Task A_custom_concurrency_control_that_only_implements_the_original_method_should_still_be_used()
    {
        var custom = new OriginalMethodOnlyConcurrencyControl();
        var sut = CreateSut(custom);

        var getToken = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);
        _tokenEndpoint.Release();
        var result = await getToken;

        result.Succeeded.ShouldBeTrue(result.FailedResult?.Error);
        custom.Calls.ShouldBe(1);
    }

    private static UserTokenRequestParameters ForResource(string resource) =>
        new() { Resource = Resource.Parse(resource) };

    private UserAccessAccessTokenManager CreateSut(IUserTokenRequestConcurrencyControl? control = null) => new(
        new AccessTokenManagementMetrics(new TestMeterFactory()),
        control ?? _control,
        new RefreshTokenOnlyStore(),
        TimeProvider.System,
        Options.Create(new UserTokenManagementOptions()),
        _tokenEndpoint,
        NullLogger<UserAccessAccessTokenManager>.Instance);

    private class OriginalMethodOnlyConcurrencyControl : IUserTokenRequestConcurrencyControl
    {
        public int Calls { get; private set; }

        public Task<TokenResult<UserToken>> ExecuteWithConcurrencyControlAsync(
            UserRefreshToken key,
            Func<Task<TokenResult<UserToken>>> tokenRetriever,
            CancellationToken ct = default)
        {
            Calls++;
            return tokenRetriever();
        }
    }

    /// <summary>
    /// Always reports "no access token cached for these parameters, but here's the refresh token".
    /// </summary>
    private class RefreshTokenOnlyStore : IUserTokenStore
    {
        public Task<TokenResult<TokenForParameters>> GetTokenAsync(ClaimsPrincipal user, UserTokenRequestParameters? parameters = null, CancellationToken ct = default) =>
            Task.FromResult<TokenResult<TokenForParameters>>(
                new TokenForParameters(new UserRefreshToken(RefreshToken.Parse("shared_refresh_token"), null)));

        public Task StoreTokenAsync(ClaimsPrincipal user, UserToken token, UserTokenRequestParameters? parameters = null, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task ClearTokenAsync(ClaimsPrincipal user, UserTokenRequestParameters? parameters = null, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// Issues an access token that echoes the requested resource, but only after <see cref="Release"/>.
    /// </summary>
    private class BlockingTokenEndpoint : IOpenIdConnectUserTokenEndpoint
    {
        private readonly TaskCompletionSource _firstRequestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;

        public int Calls => _calls;
        public Task FirstRequestStarted => _firstRequestStarted.Task;
        public void Release() => _gate.TrySetResult();

        public async Task<TokenResult<UserToken>> RefreshAccessTokenAsync(UserRefreshToken userToken, UserTokenRequestParameters parameters, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _calls);
            _firstRequestStarted.TrySetResult();
            await _gate.Task;

            return new UserToken
            {
                AccessToken = AccessToken.Parse($"at_for_{parameters.Resource}_{Guid.NewGuid():N}"),
                AccessTokenType = AccessTokenType.Parse("Bearer"),
                ClientId = ClientId.Parse("client"),
                Expiration = DateTimeOffset.UtcNow.AddHours(1),
                RefreshToken = userToken.RefreshToken,
            };
        }

        public Task RevokeRefreshTokenAsync(UserRefreshToken userToken, UserTokenRequestParameters parameters, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private class TestMeterFactory : System.Diagnostics.Metrics.IMeterFactory
    {
        public System.Diagnostics.Metrics.Meter Create(System.Diagnostics.Metrics.MeterOptions options) => new(options);
        public void Dispose() { }
    }
}
