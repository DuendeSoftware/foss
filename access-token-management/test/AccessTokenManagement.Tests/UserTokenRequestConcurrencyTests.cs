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
    private readonly InMemoryUserTokenStore _store = new("rt_0");
    private readonly BlockingTokenEndpoint _tokenEndpoint = new();
    private readonly CapturingLogger<UserTokenRequestConcurrencyControl> _controlLogger = new();
    private readonly UserTokenRequestConcurrencyControl _control;

    public UserTokenRequestConcurrencyTests() => _control = new UserTokenRequestConcurrencyControl(_controlLogger);

    /// <summary>
    /// If the token service returns a refresh token that an unrelated chain already uses (which should never
    /// happen), the chain that received it must not take it over: that chain's requests could belong to
    /// another session.
    /// </summary>
    [Fact]
    public async Task A_new_refresh_token_that_another_chain_already_uses_should_not_be_taken_over()
    {
        var endpoint = new CollidingTokenEndpoint("rt_collide");
        var sessionX = CreateSut(store: new InMemoryUserTokenStore("rt_collide"), endpoint: endpoint);
        var sessionYStore = new InMemoryUserTokenStore("rt_y");
        var sessionY = CreateSut(store: sessionYStore, endpoint: endpoint);

        // Session X refreshes res1 with rt_collide (call 0, held).
        var x1 = sessionX.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);
        await endpoint.Entered(0).WaitAsync(_ct);

        // Session Y refreshes res1 with rt_y (call 1) and gets rt_collide back; Y2 waits on Y's chain.
        var y1 = sessionY.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);
        await endpoint.Entered(1).WaitAsync(_ct);
        var y2 = sessionY.GetAccessTokenAsync(_user, ForResource("urn:res2"), _ct);
        endpoint.Release(1);
        (await y1).Succeeded.ShouldBeTrue();
        await endpoint.Entered(2).WaitAsync(_ct);

        // Another session X request for res1 must still join session X's request, not session Y's chain.
        var x2 = sessionX.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);

        endpoint.Release(0);
        endpoint.Release(2);
        var x1Result = await x1;
        var x2Result = await x2;
        (await y2).Succeeded.ShouldBeTrue();

        x2Result.Token!.AccessToken.ShouldBe(x1Result.Token!.AccessToken);
        endpoint.Calls.ShouldBe(3);
        endpoint.Presented.ShouldBe(["rt_collide", "rt_y", "rt_collide"]);
        _controlLogger.Entries.ShouldContain(e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning);
        _controlLogger.Entries.ShouldNotContain(e => e.Message.Contains("rt_"));
    }

    /// <summary>
    /// Every call is held until <see cref="Release"/>, and every call returns the same refresh token.
    /// </summary>
    private class CollidingTokenEndpoint(string refreshToken) : IOpenIdConnectUserTokenEndpoint
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, TaskCompletionSource> _entered = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, TaskCompletionSource> _released = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> _presented = new();
        private int _calls = -1;

        public int Calls => _calls + 1;
        public string[] Presented => _presented.OrderBy(p => p.Key).Select(p => p.Value).ToArray();
        public Task Entered(int call) => Gate(_entered, call).Task;
        public void Release(int call) => Gate(_released, call).TrySetResult();

        private static TaskCompletionSource Gate(System.Collections.Concurrent.ConcurrentDictionary<int, TaskCompletionSource> gates, int call) =>
            gates.GetOrAdd(call, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

        public async Task<TokenResult<UserToken>> RefreshAccessTokenAsync(UserRefreshToken userToken, UserTokenRequestParameters parameters, CancellationToken ct = default)
        {
            var call = Interlocked.Increment(ref _calls);
            _presented[call] = userToken.RefreshToken.ToString();
            Gate(_entered, call).TrySetResult();
            await Gate(_released, call).Task.WaitAsync(ct);

            return new UserToken
            {
                AccessToken = AccessToken.Parse($"at_{call}"),
                AccessTokenType = AccessTokenType.Parse("Bearer"),
                ClientId = ClientId.Parse("client"),
                Expiration = DateTimeOffset.UtcNow.AddHours(1),
                RefreshToken = RefreshToken.Parse(refreshToken),
            };
        }

        public Task RevokeRefreshTokenAsync(UserRefreshToken userToken, UserTokenRequestParameters parameters, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private class CapturingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public System.Collections.Concurrent.ConcurrentQueue<(Microsoft.Extensions.Logging.LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Enqueue((logLevel, formatter(state, exception)));
    }

    /// <summary>
    /// A request that reads a new refresh token from a shared store, while the request that stored it is
    /// still finishing, must wait on the same chain and not redeem that refresh token on its own.
    /// </summary>
    [Fact]
    public async Task A_request_that_reads_a_just_stored_refresh_token_should_wait_on_the_same_chain()
    {
        var endpoint = new SteppedRotatingTokenEndpoint();
        _store.HoldFirstStore = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sut = CreateSut(endpoint: endpoint);

        // A redeems rt_0; C (rt_0, res3) queues behind it on the chain.
        var a = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);
        await endpoint.Entered(0).WaitAsync(_ct);
        var c = sut.GetAccessTokenAsync(_user, ForResource("urn:res3"), _ct);

        // A gets rt_1 and stores it, then pauses before it finishes.
        endpoint.Release(0);
        await _store.FirstStored.Task.WaitAsync(_ct);

        // B reads rt_1 from the store while A is still finishing.
        var b = sut.GetAccessTokenAsync(_user, ForResource("urn:res2"), _ct);

        endpoint.Release(1);
        endpoint.Release(2);
        _store.HoldFirstStore.SetResult();

        var results = await Task.WhenAll(a, b, c);

        results.ShouldAllBe(r => r.Succeeded);
        endpoint.RedeemedRefreshTokens.ShouldBe(["rt_0", "rt_1", "rt_2"]);
    }

    /// <summary>
    /// When storing fails after the token endpoint rotated the refresh token, the next request must redeem
    /// the new refresh token, not the one that was already used.
    /// </summary>
    [Fact]
    public async Task A_failed_store_should_not_make_the_next_request_redeem_the_used_refresh_token()
    {
        var endpoint = new SteppedRotatingTokenEndpoint();
        var request1Store = new InMemoryUserTokenStore("rt_0") { ThrowOnFirstStore = true };
        var request2Store = new InMemoryUserTokenStore("rt_0");
        var request1 = CreateSut(store: request1Store, endpoint: endpoint);
        var request2 = CreateSut(store: request2Store, endpoint: endpoint);

        var a = request1.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);
        await endpoint.Entered(0).WaitAsync(_ct);
        var b = request2.GetAccessTokenAsync(_user, ForResource("urn:res2"), _ct);

        endpoint.Release(0);
        await Should.ThrowAsync<InvalidOperationException>(() => a);
        endpoint.Release(1);
        var bResult = await b;

        bResult.Succeeded.ShouldBeTrue(bResult.FailedResult?.Error);
        endpoint.RedeemedRefreshTokens.ShouldBe(["rt_0", "rt_1"]);
    }

    [Fact]
    public async Task A_caller_that_cancels_should_not_cancel_the_shared_request_for_the_other_callers()
    {
        var endpoint = new SteppedRotatingTokenEndpoint();
        var sut = CreateSut(endpoint: endpoint);
        using var firstCallerCts = CancellationTokenSource.CreateLinkedTokenSource(_ct);

        var first = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), firstCallerCts.Token);
        await endpoint.Entered(0).WaitAsync(_ct);
        var second = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);

        await firstCallerCts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => first);

        endpoint.Release(0);
        var secondResult = await second;

        secondResult.Succeeded.ShouldBeTrue(secondResult.FailedResult?.Error);
        endpoint.RedeemedRefreshTokens.ShouldBe(["rt_0"]);
        endpoint.CancelledCalls.ShouldBeEmpty();
    }

    // Request 1 (res1) holds the chain, request 2 (res2) is queued behind it, then request 1 is cancelled.
    // Request 2 must take over, whatever request 1 was doing at that moment, without redeeming a used token.

    [Fact]
    public async Task Cancelled_before_the_token_service_processed_the_request_queued_request_should_take_over()
    {
        var endpoint = new SteppedRotatingTokenEndpoint();
        var sut = CreateSut(endpoint: endpoint);
        using var request1Cts = CancellationTokenSource.CreateLinkedTokenSource(_ct);

        var request1 = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), request1Cts.Token);
        await endpoint.Entered(0).WaitAsync(_ct);
        var request2 = sut.GetAccessTokenAsync(_user, ForResource("urn:res2"), _ct);

        await request1Cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => request1);
        endpoint.Release(0);
        endpoint.Release(1);
        var result2 = await request2;

        result2.Succeeded.ShouldBeTrue(result2.FailedResult?.Error);
        endpoint.RedeemedRefreshTokens.ShouldBe(endpoint.RedeemedRefreshTokens.Distinct());
    }

    [Fact]
    public async Task Cancelled_after_the_token_service_rotated_the_refresh_token_queued_request_should_not_redeem_the_used_token()
    {
        var endpoint = new SteppedRotatingTokenEndpoint { ProcessBeforeResponse = true };
        var sut = CreateSut(endpoint: endpoint);
        using var request1Cts = CancellationTokenSource.CreateLinkedTokenSource(_ct);

        // The token service has rotated rt_0 to rt_1; the response is still on its way.
        var request1 = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), request1Cts.Token);
        await endpoint.Entered(0).WaitAsync(_ct);
        var request2 = sut.GetAccessTokenAsync(_user, ForResource("urn:res2"), _ct);

        await request1Cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => request1);
        endpoint.Release(0);
        endpoint.Release(1);
        var result2 = await request2;

        result2.Succeeded.ShouldBeTrue(result2.FailedResult?.Error);
        endpoint.RedeemedRefreshTokens.ShouldBe(["rt_0", "rt_1"]);
    }

    [Fact]
    public async Task Cancelled_while_storing_the_queued_request_should_use_the_new_refresh_token()
    {
        var endpoint = new SteppedRotatingTokenEndpoint();
        _store.HoldFirstStore = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sut = CreateSut(endpoint: endpoint);
        using var request1Cts = CancellationTokenSource.CreateLinkedTokenSource(_ct);

        var request1 = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), request1Cts.Token);
        await endpoint.Entered(0).WaitAsync(_ct);
        var request2 = sut.GetAccessTokenAsync(_user, ForResource("urn:res2"), _ct);
        endpoint.Release(0);
        await _store.FirstStored.Task.WaitAsync(_ct);

        await request1Cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => request1);
        _store.HoldFirstStore.SetResult();
        endpoint.Release(1);
        var result2 = await request2;

        result2.Succeeded.ShouldBeTrue(result2.FailedResult?.Error);
        endpoint.RedeemedRefreshTokens.ShouldBe(["rt_0", "rt_1"]);
    }

    [Fact]
    public async Task The_shared_request_should_be_cancelled_when_all_callers_cancel()
    {
        var endpoint = new SteppedRotatingTokenEndpoint();
        var sut = CreateSut(endpoint: endpoint);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_ct);

        var first = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), cts.Token);
        await endpoint.Entered(0).WaitAsync(_ct);
        var second = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), cts.Token);

        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => first);
        await Should.ThrowAsync<OperationCanceledException>(() => second);

        await endpoint.Cancelled(0).WaitAsync(_ct);
        await WaitUntilAsync(() => _control.ActiveRefreshTokenCount == 0);
    }

    [Fact]
    public async Task Chains_should_be_forgotten_once_no_requests_use_them()
    {
        var endpoint = new SteppedRotatingTokenEndpoint();
        var sut = CreateSut(endpoint: endpoint);

        var a = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);
        await endpoint.Entered(0).WaitAsync(_ct);
        var b = sut.GetAccessTokenAsync(_user, ForResource("urn:res2"), _ct);

        _control.ActiveRefreshTokenCount.ShouldBe(1); // rt_0
        endpoint.Release(0);
        await a;
        await endpoint.Entered(1).WaitAsync(_ct);
        _control.ActiveRefreshTokenCount.ShouldBe(2); // rt_0, rt_1
        endpoint.Release(1);
        await b;

        _control.ActiveRefreshTokenCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_custom_concurrency_control_that_only_implements_the_original_method_should_still_be_used()
    {
        var custom = new OriginalMethodOnlyConcurrencyControl();
        var sut = CreateSut(control: custom);

        var getToken = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);
        _tokenEndpoint.Release();
        var result = await getToken;

        result.Succeeded.ShouldBeTrue(result.FailedResult?.Error);
        custom.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task The_obsolete_method_should_keep_merging_on_the_refresh_token_alone()
    {
        var refreshToken = new UserRefreshToken(RefreshToken.Parse("rt_0"), null);
        var gate = new TaskCompletionSource<TokenResult<UserToken>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;

#pragma warning disable CS0618 // The obsolete method is what this test is about.
        var first = _control.ExecuteWithConcurrencyControlAsync(refreshToken, () => { calls++; return gate.Task; }, _ct);
        var second = _control.ExecuteWithConcurrencyControlAsync(refreshToken, () => { calls++; return gate.Task; }, _ct);
#pragma warning restore CS0618

        gate.SetResult(new UserToken
        {
            AccessToken = AccessToken.Parse("at_for_first"),
            AccessTokenType = AccessTokenType.Parse("Bearer"),
            ClientId = ClientId.Parse("client"),
            Expiration = DateTimeOffset.UtcNow.AddHours(1),
        });

        (await first).Token!.AccessToken.ShouldBe((await second).Token!.AccessToken);
        calls.ShouldBe(1);
        _control.ActiveRefreshTokenCount.ShouldBe(0); // it doesn't take part in the chains
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        condition().ShouldBeTrue();
    }

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

    [Fact]
    public async Task Concurrent_refreshes_for_different_resources_should_each_get_their_own_token()
    {
        var sut = CreateSut();

        // First call starts a refresh for res1 and is held inside the token endpoint.
        var res1Call = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);
        await _tokenEndpoint.FirstRequestStarted.WaitAsync(_ct);

        // Second call, same user / same refresh token, but a different resource.
        var res2Call = sut.GetAccessTokenAsync(_user, ForResource("urn:res2"), _ct);

        _tokenEndpoint.Release();

        var res1 = await res1Call;
        var res2 = await res2Call;

        res1.Token!.AccessToken.ToString().ShouldStartWith("at_for_urn:res1");
        res2.Token!.AccessToken.ToString().ShouldStartWith("at_for_urn:res2");
    }

    [Fact]
    public async Task Concurrent_refreshes_for_different_resources_should_not_redeem_a_rotated_refresh_token_twice()
    {
        _tokenEndpoint.RotateRefreshTokens = true;
        var sut = CreateSut();

        var res1Call = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);
        await _tokenEndpoint.FirstRequestStarted.WaitAsync(_ct);
        var res2Call = sut.GetAccessTokenAsync(_user, ForResource("urn:res2"), _ct);

        _tokenEndpoint.Release();

        var res1 = await res1Call;
        var res2 = await res2Call;

        res1.Succeeded.ShouldBeTrue(res1.FailedResult?.Error);
        res2.Succeeded.ShouldBeTrue(res2.FailedResult?.Error);
        _tokenEndpoint.RedeemedRefreshTokens.ShouldBe(["rt_0", "rt_1"]);
    }

    [Fact]
    public async Task Concurrent_refreshes_for_the_same_parameters_should_share_one_token_request()
    {
        var sut = CreateSut();

        var first = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);
        await _tokenEndpoint.FirstRequestStarted.WaitAsync(_ct);
        var second = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);

        _tokenEndpoint.Release();

        var firstResult = await first;
        var secondResult = await second;

        _tokenEndpoint.RedeemedRefreshTokens.Count.ShouldBe(1);
        secondResult.Token!.AccessToken.ShouldBe(firstResult.Token!.AccessToken);
    }

    /// <summary>
    /// A holds lock "rt_0" and rotates it to rt_1. B, which waited on lock "rt_0", sends rt_1.
    /// C arrives while B's request is running, reads rt_1 and locks on "rt_1": a different lock.
    /// Both B and C present rt_1, so the one that is processed second gets invalid_grant.
    /// </summary>
    [Fact]
    public async Task A_request_that_reads_a_rotated_refresh_token_should_wait_for_the_request_already_redeeming_it()
    {
        var endpoint = new SteppedRotatingTokenEndpoint();
        var sut = CreateSut(endpoint: endpoint);

        // A redeems rt_0; B (different resource) waits on the lock for rt_0.
        var a = sut.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);
        await endpoint.Entered(0).WaitAsync(_ct);
        var b = sut.GetAccessTokenAsync(_user, ForResource("urn:res2"), _ct);

        // A finishes: rt_0 rotates to rt_1. B takes the lock, re-reads rt_1 and sends it.
        endpoint.Release(0);
        (await a).Succeeded.ShouldBeTrue();
        await endpoint.Entered(1).WaitAsync(_ct);

        // C arrives while B's request with rt_1 is still running.
        var c = sut.GetAccessTokenAsync(_user, ForResource("urn:res3"), _ct);

        endpoint.Release(1);
        var bResult = await b;
        endpoint.Release(2);
        var cResult = await c;

        bResult.Succeeded.ShouldBeTrue(bResult.FailedResult?.Error);
        cResult.Succeeded.ShouldBeTrue(cResult.FailedResult?.Error);
        endpoint.RedeemedRefreshTokens.ShouldBe(["rt_0", "rt_1", "rt_2"]);
    }

    /// <summary>
    /// Two HTTP requests arrive with the same cookie (rt_0). The default store reads the cookie of the
    /// current request, so each request has its own view. A redeems rt_0 and gets rt_1, but only request 1
    /// sees rt_1. B, which waited on the shared lock, re-reads request 2's cookie and redeems rt_0 again.
    /// </summary>
    [Fact]
    public async Task Concurrent_http_requests_with_the_same_cookie_should_not_redeem_a_rotated_refresh_token_twice()
    {
        var endpoint = new SteppedRotatingTokenEndpoint();
        var control = _control; // singleton in DI, shared by both requests
        var request1 = CreateSut(store: new InMemoryUserTokenStore("rt_0"), endpoint: endpoint, control: control);
        var request2 = CreateSut(store: new InMemoryUserTokenStore("rt_0"), endpoint: endpoint, control: control);

        var a = request1.GetAccessTokenAsync(_user, ForResource("urn:res1"), _ct);
        await endpoint.Entered(0).WaitAsync(_ct);
        var b = request2.GetAccessTokenAsync(_user, ForResource("urn:res2"), _ct);

        endpoint.Release(0);
        (await a).Succeeded.ShouldBeTrue();
        endpoint.Release(1);
        var bResult = await b;

        bResult.Succeeded.ShouldBeTrue(bResult.FailedResult?.Error);
        endpoint.RedeemedRefreshTokens.ShouldBe(["rt_0", "rt_1"]);
    }

    private static UserTokenRequestParameters ForResource(string resource) =>
        new() { Resource = Resource.Parse(resource) };

    private UserAccessAccessTokenManager CreateSut(
        IUserTokenStore? store = null,
        IOpenIdConnectUserTokenEndpoint? endpoint = null,
        IUserTokenRequestConcurrencyControl? control = null) => new(
        new AccessTokenManagementMetrics(new TestMeterFactory()),
        control ?? _control,
        store ?? _store,
        TimeProvider.System,
        Options.Create(new UserTokenManagementOptions()),
        endpoint ?? _tokenEndpoint,
        NullLogger<UserAccessAccessTokenManager>.Instance);

    /// <summary>
    /// A token endpoint with one-time-use (rotating) refresh tokens that the test steps through call by call:
    /// call <c>n</c> signals <see cref="Entered"/>(n) and is processed only after <see cref="Release"/>(n).
    /// </summary>
    private class SteppedRotatingTokenEndpoint : IOpenIdConnectUserTokenEndpoint
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, TaskCompletionSource> _entered = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, TaskCompletionSource> _released = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, TaskCompletionSource> _cancelled = new();
        private readonly Lock _lock = new();
        private int _calls = -1;
        private int _rotation;

        public List<string> RedeemedRefreshTokens { get; } = [];
        public System.Collections.Concurrent.ConcurrentBag<int> CancelledCalls { get; } = [];
        public Task Entered(int call) => Gate(_entered, call).Task;
        public Task Cancelled(int call) => Gate(_cancelled, call).Task;
        public void Release(int call) => Gate(_released, call).TrySetResult();

        private static TaskCompletionSource Gate(System.Collections.Concurrent.ConcurrentDictionary<int, TaskCompletionSource> gates, int call) =>
            gates.GetOrAdd(call, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

        public async Task<TokenResult<UserToken>> RefreshAccessTokenAsync(UserRefreshToken userToken, UserTokenRequestParameters parameters, CancellationToken ct = default)
        {
            var call = Interlocked.Increment(ref _calls);

            // The token service processes the request before the response is held: a cancellation while held
            // loses a response for a refresh token that is already rotated.
            var processed = ProcessBeforeResponse ? Process(userToken, parameters) : null;

            Gate(_entered, call).TrySetResult();
            try
            {
                await Gate(_released, call).Task.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                CancelledCalls.Add(call);
                Gate(_cancelled, call).TrySetResult();
                throw;
            }

            return processed ?? Process(userToken, parameters);
        }

        /// <summary>When set, the token service processes the request as soon as it arrives.</summary>
        public bool ProcessBeforeResponse { get; set; }

        private TokenResult<UserToken> Process(UserRefreshToken userToken, UserTokenRequestParameters parameters)
        {
            RefreshToken newRefreshToken;
            lock (_lock)
            {
                var presented = userToken.RefreshToken.ToString();
                if (presented != $"rt_{_rotation}")
                {
                    return TokenResult.Failure($"invalid_grant: {presented} was already used");
                }

                RedeemedRefreshTokens.Add(presented);
                newRefreshToken = RefreshToken.Parse($"rt_{++_rotation}");
            }

            return new UserToken
            {
                AccessToken = AccessToken.Parse($"at_for_{parameters.Resource}_{Guid.NewGuid():N}"),
                AccessTokenType = AccessTokenType.Parse("Bearer"),
                ClientId = ClientId.Parse("client"),
                Expiration = DateTimeOffset.UtcNow.AddHours(1),
                RefreshToken = newRefreshToken,
            };
        }

        public Task RevokeRefreshTokenAsync(UserRefreshToken userToken, UserTokenRequestParameters parameters, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// Like the default store: one access token per resource, one refresh token shared by all resources.
    /// </summary>
    private class InMemoryUserTokenStore(string refreshToken) : IUserTokenStore
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<string, UserToken> _accessTokens = new();
        private RefreshToken _refreshToken = RefreshToken.Parse(refreshToken);

        public Task<TokenResult<TokenForParameters>> GetTokenAsync(ClaimsPrincipal user, UserTokenRequestParameters? parameters = null, CancellationToken ct = default)
        {
            lock (_lock)
            {
                var refresh = new UserRefreshToken(_refreshToken, null);
                TokenForParameters result = _accessTokens.TryGetValue(ResourceKey(parameters), out var token)
                    ? new TokenForParameters(token, refresh)
                    : new TokenForParameters(refresh);
                return Task.FromResult<TokenResult<TokenForParameters>>(result);
            }
        }

        /// <summary>When set, the first store writes the token and then waits for this before returning.</summary>
        public TaskCompletionSource? HoldFirstStore { get; set; }

        /// <summary>Completes when the first store has written its token.</summary>
        public TaskCompletionSource FirstStored { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>When set, the first store throws without writing, like a cookie that can't be set anymore.</summary>
        public bool ThrowOnFirstStore { get; set; }

        private int _stores;

        public async Task StoreTokenAsync(ClaimsPrincipal user, UserToken token, UserTokenRequestParameters? parameters = null, CancellationToken ct = default)
        {
            var first = Interlocked.Increment(ref _stores) == 1;
            if (first && ThrowOnFirstStore)
            {
                throw new InvalidOperationException("Can't store the token.");
            }

            lock (_lock)
            {
                _accessTokens[ResourceKey(parameters)] = token;
                _refreshToken = token.RefreshToken ?? _refreshToken;
            }

            if (first)
            {
                FirstStored.TrySetResult();
                if (HoldFirstStore != null)
                {
                    await HoldFirstStore.Task;
                }
            }
        }

        public Task ClearTokenAsync(ClaimsPrincipal user, UserTokenRequestParameters? parameters = null, CancellationToken ct = default) =>
            Task.CompletedTask;

        private static string ResourceKey(UserTokenRequestParameters? parameters) => parameters?.Resource?.ToString() ?? "";
    }

    /// <summary>
    /// Issues an access token that echoes the requested resource. The first request is held until <see cref="Release"/>.
    /// When <see cref="RotateRefreshTokens"/> is set, each refresh token can be redeemed only once.
    /// </summary>
    private class BlockingTokenEndpoint : IOpenIdConnectUserTokenEndpoint
    {
        private readonly TaskCompletionSource _firstRequestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Lock _lock = new();
        private int _rotation;

        public bool RotateRefreshTokens { get; set; }
        public List<string> RedeemedRefreshTokens { get; } = [];
        public Task FirstRequestStarted => _firstRequestStarted.Task;
        public void Release() => _gate.TrySetResult();

        public async Task<TokenResult<UserToken>> RefreshAccessTokenAsync(UserRefreshToken userToken, UserTokenRequestParameters parameters, CancellationToken ct = default)
        {
            _firstRequestStarted.TrySetResult();
            await _gate.Task;

            RefreshToken newRefreshToken;
            lock (_lock)
            {
                var presented = userToken.RefreshToken.ToString();
                if (RotateRefreshTokens && presented != $"rt_{_rotation}")
                {
                    return TokenResult.Failure("invalid_grant");
                }

                RedeemedRefreshTokens.Add(presented);
                if (RotateRefreshTokens)
                {
                    _rotation++;
                }

                newRefreshToken = RefreshToken.Parse($"rt_{_rotation}");
            }

            return new UserToken
            {
                AccessToken = AccessToken.Parse($"at_for_{parameters.Resource}_{Guid.NewGuid():N}"),
                AccessTokenType = AccessTokenType.Parse("Bearer"),
                ClientId = ClientId.Parse("client"),
                Expiration = DateTimeOffset.UtcNow.AddHours(1),
                RefreshToken = newRefreshToken,
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
