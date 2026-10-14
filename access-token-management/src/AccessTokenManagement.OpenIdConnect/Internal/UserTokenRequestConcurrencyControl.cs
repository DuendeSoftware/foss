// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text;
using Duende.AccessTokenManagement.Internal;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

/// <summary>
/// Default implementation for token request concurrency control, within one process.
/// <list type="bullet">
/// <item>A refresh token and every refresh token it rotates into form a chain. Requests on the same chain
/// run one at a time, and each redeems the newest refresh token of the chain, so a refresh token value is
/// redeemed at most once.</item>
/// <item>Concurrent requests on the same chain with the same parameters share one result.</item>
/// <item>Requests with different parameters (for example a different resource) each get their own result.</item>
/// <item>A caller that cancels stops waiting. When all callers of a request have cancelled: a queued request is
/// dropped; a request at the token service is cancelled if it's the only request on its chain, and otherwise
/// finishes so the next request on the chain gets the new refresh token. A result nobody waits for isn't stored.</item>
/// </list>
/// A chain is forgotten as soon as no requests are using it.
/// </summary>
internal class UserTokenRequestConcurrencyControl(ILogger<UserTokenRequestConcurrencyControl> logger) : IUserTokenRequestConcurrencyControl
{
    // This is what provides the synchronization; assumes this service is a singleton in DI.
    private readonly Lock _lock = new();

    // Every refresh token value of an active chain points at that chain.
    private readonly Dictionary<string, Chain> _chains = new(StringComparer.Ordinal);

    // The original method's in-flight requests, merged on the refresh token alone, exactly as before the
    // overload with parameters existed. Kept separate from the chains, see that method.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<Task<TokenResult<UserToken>>>> _legacyInFlight = new();

    /// <summary>
    /// The number of refresh token values that point at an active chain. For tests.
    /// </summary>
    internal int ActiveRefreshTokenCount
    {
        get
        {
            lock (_lock)
            {
                return _chains.Count;
            }
        }
    }

    /// <summary>
    /// The number of callers waiting for a request that still takes callers. For tests.
    /// </summary>
    internal int JoinableCallerCount
    {
        get
        {
            lock (_lock)
            {
                return _chains.Values.Distinct().SelectMany(c => c.InFlight.Values).Sum(r => r.ActiveWaiters);
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Behaves as it did before the overload with parameters was added: concurrent calls with the same refresh
    /// token share one result, whatever they request, and they don't take part in the chains. The retriever
    /// chooses the refresh token and stores the result itself, so this method can't guarantee that a rotated
    /// refresh token is redeemed at most once. The library doesn't call it.
    /// </remarks>
    [Obsolete(IUserTokenRequestConcurrencyControl.ObsoleteMessage)]
    public async Task<TokenResult<UserToken>> ExecuteWithConcurrencyControlAsync(
        UserRefreshToken key,
        Func<Task<TokenResult<UserToken>>> tokenRetriever,
        CT ct = default)
    {
        var refreshToken = key.RefreshToken.ToString();
        var request = _legacyInFlight.GetOrAdd(refreshToken, _ => new Lazy<Task<TokenResult<UserToken>>>(tokenRetriever));
        try
        {
            return await request.Value.ConfigureAwait(false);
        }
        finally
        {
            _legacyInFlight.TryRemove(new KeyValuePair<string, Lazy<Task<TokenResult<UserToken>>>>(refreshToken, request));
        }
    }

    /// <inheritdoc/>
    public async Task<TokenResult<UserToken>> ExecuteWithConcurrencyControlAsync(
        UserRefreshToken key,
        UserTokenRequestParameters parameters,
        Func<UserRefreshToken, CT, Task<TokenResult<UserToken>>> redeem,
        Func<UserToken, CT, Task> store,
        CT ct = default)
    {
        var requestKey = BuildRequestKey(parameters);
        InFlightRequest request;
        var waiter = new Waiter(store);

        lock (_lock)
        {
            var refreshToken = key.RefreshToken.ToString();
            if (!_chains.TryGetValue(refreshToken, out var chain))
            {
                chain = new Chain(key);
                chain.Aliases.Add(refreshToken);
                _chains[refreshToken] = chain;
            }

            if (!chain.InFlight.TryGetValue(requestKey, out request!))
            {
                chain.RefCount++;
                request = new InFlightRequest(chain, requestKey);
                var created = request;
                request.Work = new Lazy<Task<TokenResult<UserToken>>>(() => RunAsync(created, redeem));
                chain.InFlight[requestKey] = request;
            }

            request.Waiters.Add(waiter);
            request.ActiveWaiters++;
        }

        try
        {
            var result = await request.Work.Value.WaitAsync(ct).ConfigureAwait(false);

            // Storing is per caller: if storing into this caller's context failed, only this caller fails.
            if (waiter.StoreError != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(waiter.StoreError);
            }

            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Leave(request, waiter);

            // The work may still fail after everyone left (for example a cancelled token service call).
            // Observe it, so it doesn't surface as an unobserved task exception.
            _ = request.Work.Value.ContinueWith(
                static t => _ = t.Exception,
                CT.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            throw;
        }
    }

    /// <summary>
    /// A caller stopped waiting. When no callers are left, make sure new callers don't join the request, and:
    /// <list type="bullet">
    /// <item>if it is still queued, drop it;</item>
    /// <item>if it is at the token service and no other request is on the chain, cancel that call, because nobody
    /// could use its result;</item>
    /// <item>if it is at the token service and other requests are on the chain, let it finish, so they get the new
    /// refresh token instead of presenting one that may already be used.</item>
    /// </list>
    /// The decision is made under the same lock that <see cref="RunAsync"/> uses to start the request, so a
    /// request is never both dropped and started.
    /// </summary>
    private void Leave(InFlightRequest request, Waiter waiter)
    {
        CancellationTokenSource? toCancel = null;
        lock (_lock)
        {
            waiter.Left = true;

            if (--request.ActiveWaiters > 0)
            {
                return;
            }

            if (request.Chain.InFlight.TryGetValue(request.Key, out var current) && ReferenceEquals(current, request))
            {
                request.Chain.InFlight.Remove(request.Key);
            }

            if (!request.Started)
            {
                request.Abandoned = true;
                toCancel = request.WaitCancellation;
            }
            else if (request.Chain.RefCount == 1)
            {
                toCancel = request.RedeemCancellation;
            }
        }

        // Outside the lock: cancellation callbacks run synchronously.
        toCancel?.Cancel();
    }

    private async Task<TokenResult<UserToken>> RunAsync(
        InFlightRequest request,
        Func<UserRefreshToken, CT, Task<TokenResult<UserToken>>> redeem)
    {
        var chain = request.Chain;
        var requestKey = request.Key;
        try
        {
            await chain.Semaphore.WaitAsync(request.WaitCancellation.Token).ConfigureAwait(false);
            try
            {
                UserRefreshToken refreshToken;
                lock (_lock)
                {
                    // Everyone may have stopped waiting while this request was queued: nothing was sent yet,
                    // so it's safe to stop here.
                    if (request.Abandoned)
                    {
                        throw new OperationCanceledException(request.WaitCancellation.Token);
                    }

                    request.Started = true;
                    refreshToken = chain.Latest;
                }

                var result = await redeem(refreshToken, request.RedeemCancellation.Token).ConfigureAwait(false);
                if (!result.WasSuccessful(out var token))
                {
                    return result;
                }

                // Record the new refresh token before it is stored: once it is in the store, other requests can
                // read it, and they must find this chain. If storing fails, the chain still knows the newest token.
                if (token.RefreshToken is { } newRefreshToken && newRefreshToken != refreshToken.RefreshToken)
                {
                    bool collision;
                    lock (_lock)
                    {
                        chain.Latest = new UserRefreshToken(newRefreshToken, token.DPoPJsonWebKey ?? refreshToken.DPoPProofKey);
                        var alias = newRefreshToken.ToString();

                        // The token service just issued this value, so no other chain should know it. If another
                        // chain does, leave it there: taking it over could hand this chain's tokens to the other
                        // chain's requests, which may belong to another session.
                        collision = _chains.TryGetValue(alias, out var owner) && !ReferenceEquals(owner, chain);
                        if (!collision)
                        {
                            _chains[alias] = chain;
                            if (!chain.Aliases.Contains(alias))
                            {
                                chain.Aliases.Add(alias);
                            }
                        }
                    }

                    if (collision)
                    {
                        logger.RefreshTokenAlreadyUsedByAnotherRefresh(LogLevel.Warning);
                    }
                }

                // Every caller still waiting stores the result into its own context (for example its own HTTP
                // response), one after another while this chain is locked, so a shared store sees the refresh
                // tokens in order. Callers that left are skipped: their context may be gone. From here on the
                // request takes no new callers, because they would miss the stores; they start a new request,
                // which uses the new refresh token.
                List<Waiter> waiters;
                lock (_lock)
                {
                    if (chain.InFlight.TryGetValue(requestKey, out var inFlight) && ReferenceEquals(inFlight, request))
                    {
                        chain.InFlight.Remove(requestKey);
                    }

                    waiters = [.. request.Waiters];
                }

                foreach (var waiter in waiters)
                {
                    lock (_lock)
                    {
                        if (waiter.Left)
                        {
                            continue;
                        }
                    }

                    try
                    {
                        await StoreInCallerContext(waiter, token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // Reported to this caller only; if it has left, nobody needs this store.
                        waiter.StoreError = ex;
                    }
                }

                return result;
            }
            finally
            {
                chain.Semaphore.Release();
            }
        }
        finally
        {
            lock (_lock)
            {
                if (chain.InFlight.TryGetValue(requestKey, out var current) && ReferenceEquals(current, request))
                {
                    chain.InFlight.Remove(requestKey);
                }

                if (--chain.RefCount == 0)
                {
                    foreach (var alias in chain.Aliases)
                    {
                        if (_chains.TryGetValue(alias, out var c) && ReferenceEquals(c, chain))
                        {
                            _chains.Remove(alias);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Runs a caller's store in that caller's own execution context. Stores find their request through ambient
    /// state (the default store uses IHttpContextAccessor), and <see cref="RunAsync"/> runs in the context of
    /// whichever caller started the request.
    /// </summary>
    private static Task StoreInCallerContext(Waiter waiter, UserToken token)
    {
        if (waiter.Context == null)
        {
            // The caller suppressed context flow, so its context can't be restored. Run the store in an empty
            // context rather than in the current one, which belongs to another caller: a store that needs the
            // caller's request then fails for this caller, instead of writing through another caller's request.
            if (ExecutionContext.IsFlowSuppressed())
            {
                return Task.Run(() => waiter.Store(token, CT.None));
            }

            using (ExecutionContext.SuppressFlow())
            {
                return Task.Run(() => waiter.Store(token, CT.None));
            }
        }

        Task? storing = null;
        ExecutionContext.Run(waiter.Context, _ => storing = waiter.Store(token, CT.None), null);
        return storing!;
    }

    /// <summary>
    /// Builds a key that is equal for requests on the same chain that would result in the same token request.
    /// </summary>
    private static string BuildRequestKey(UserTokenRequestParameters parameters)
    {
        var sb = new StringBuilder();
        Append(sb, parameters.Resource?.ToString());
        Append(sb, parameters.Scope?.ToString());
        Append(sb, parameters.ChallengeScheme?.ToString());
        Append(sb, parameters.SignInScheme?.ToString());
        foreach (var parameter in parameters.Parameters)
        {
            Append(sb, parameter.Key);
            Append(sb, parameter.Value);
        }

        return sb.ToString();

        // Length-prefix every value, so no combination of values can produce the same key as another.
        static void Append(StringBuilder sb, string? value)
        {
            if (value == null)
            {
                sb.Append("-|");
            }
            else
            {
                sb.Append(value.Length).Append(':').Append(value).Append('|');
            }
        }
    }

    /// <summary>
    /// A refresh token and every refresh token it was rotated into.
    /// </summary>
    private sealed class Chain(UserRefreshToken latest)
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public UserRefreshToken Latest { get; set; } = latest;
        public List<string> Aliases { get; } = [];
        public Dictionary<string, InFlightRequest> InFlight { get; } = new(StringComparer.Ordinal);
        public int RefCount { get; set; }
    }

    /// <summary>
    /// A caller waiting for a request, with the delegate that stores the result into that caller's context.
    /// </summary>
    private sealed class Waiter(Func<UserToken, CT, Task> store)
    {
        public Func<UserToken, CT, Task> Store { get; } = store;

        // The caller's execution context, so its store runs against its own ambient state (its HTTP request).
        public ExecutionContext? Context { get; } = ExecutionContext.Capture();

        // Guarded by the control's lock.
        public bool Left { get; set; }

        // Written before the request's task completes, read by the caller after awaiting it.
        public Exception? StoreError { get; set; }
    }

    private sealed class InFlightRequest(Chain chain, string key)
    {
        public Chain Chain { get; } = chain;
        public string Key { get; } = key;
        public Lazy<Task<TokenResult<UserToken>>> Work { get; set; } = null!;

        // Neither is disposed on purpose: they have no timers or linked tokens, and Leave may still call Cancel.
        public CancellationTokenSource WaitCancellation { get; } = new();
        public CancellationTokenSource RedeemCancellation { get; } = new();

        // Guarded by the control's lock. Every caller that joined, and how many of them still wait.
        public List<Waiter> Waiters { get; } = [];
        public int ActiveWaiters { get; set; }

        // Guarded by the control's lock: the request was sent to the token service, or was dropped before that.
        public bool Started { get; set; }
        public bool Abandoned { get; set; }
    }
}
