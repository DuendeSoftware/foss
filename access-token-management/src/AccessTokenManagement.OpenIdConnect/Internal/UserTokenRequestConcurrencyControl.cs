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
/// <item>A caller that cancels stops waiting. The shared request is cancelled only when all its callers have cancelled.</item>
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
                request.Work = new Lazy<Task<TokenResult<UserToken>>>(() => RunAsync(created, redeem, store));
                chain.InFlight[requestKey] = request;
            }

            request.Waiters++;
        }

        try
        {
            return await request.Work.Value.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Leave(request);
            throw;
        }
    }

    /// <summary>
    /// A caller stopped waiting. When no callers are left, cancel the shared request and make sure
    /// new callers don't join it.
    /// </summary>
    private void Leave(InFlightRequest request)
    {
        lock (_lock)
        {
            if (--request.Waiters > 0)
            {
                return;
            }

            if (request.Chain.InFlight.TryGetValue(request.Key, out var current) && ReferenceEquals(current, request))
            {
                request.Chain.InFlight.Remove(request.Key);
            }
        }

        // Outside the lock: cancellation callbacks run synchronously.
        request.Cancellation.Cancel();
    }

    private async Task<TokenResult<UserToken>> RunAsync(
        InFlightRequest request,
        Func<UserRefreshToken, CT, Task<TokenResult<UserToken>>> redeem,
        Func<UserToken, CT, Task> store)
    {
        var chain = request.Chain;
        var requestKey = request.Key;
        var ct = request.Cancellation.Token;
        try
        {
            await chain.Semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                UserRefreshToken refreshToken;
                lock (_lock)
                {
                    refreshToken = chain.Latest;
                }

                var result = await redeem(refreshToken, ct).ConfigureAwait(false);
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

                await store(token, ct).ConfigureAwait(false);
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

    private sealed class InFlightRequest(Chain chain, string key)
    {
        public Chain Chain { get; } = chain;
        public string Key { get; } = key;
        public Lazy<Task<TokenResult<UserToken>>> Work { get; set; } = null!;

        // Not disposed on purpose: it has no timer or linked tokens, and Leave may still call Cancel.
        public CancellationTokenSource Cancellation { get; } = new();
        public int Waiters { get; set; }
    }
}
