// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Util;

/// <summary>
/// A <see cref="HybridCache"/> that lets a test hold a request at its cache lookup, just before the handler
/// joins the in-flight introspection call for its token, and release it at a chosen moment.
/// </summary>
internal sealed class GatedLookupCache(HybridCache inner) : HybridCache
{
    private LookupGate? _nextGate;

    /// <summary>Holds the next cache lookup at <paramref name="gate"/> until the gate is released.</summary>
    public void HoldNextLookup(LookupGate gate) => Volatile.Write(ref _nextGate, gate);

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var gate = Interlocked.Exchange(ref _nextGate, null);
        if (gate != null)
        {
            await gate.Hold();
            return default!;
        }

        return await inner.GetOrCreateAsync(key, state, factory, options, tags, cancellationToken);
    }

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default) =>
        inner.SetAsync(key, value, options, tags, cancellationToken);

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        inner.RemoveAsync(key, cancellationToken);

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) =>
        inner.RemoveByTagAsync(tag, cancellationToken);
}

/// <summary>
/// One held cache lookup. The held lookup reports a cache miss.
/// </summary>
/// <remarks>
/// <see cref="Release"/> completes the lookup with continuations running synchronously, so the held request
/// runs on the releasing thread until it awaits the shared introspection task. When <see cref="Release"/>
/// returns, that request is waiting on the call.
/// </remarks>
internal sealed class LookupGate
{
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Deliberately without RunContinuationsAsynchronously: see the remarks above.
    private readonly TaskCompletionSource _release = new();

    /// <summary>Completes once a request has reached the held lookup.</summary>
    public Task Reached => _reached.Task;

    /// <summary>Lets the held request continue.</summary>
    public void Release() => _release.SetResult();

    internal Task Hold()
    {
        _reached.SetResult();
        return _release.Task;
    }
}
