// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Util;

/// <summary>
/// A <see cref="HybridCache"/> that lets a test hold one request at its cache lookup, just before the
/// handler joins the in-flight introspection call for its token, and release it at a chosen moment.
/// </summary>
/// <remarks>
/// The held lookup reports a cache miss. <see cref="ReleaseHeldLookup"/> completes it with continuations
/// running synchronously, so the held request runs on the releasing thread until it awaits the shared
/// introspection task. When <see cref="ReleaseHeldLookup"/> returns, that request is waiting on the call.
/// </remarks>
internal sealed class GatedLookupCache(HybridCache inner) : HybridCache
{
    private readonly TaskCompletionSource _lookupReached = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Deliberately without RunContinuationsAsynchronously: see the remarks above.
    private readonly TaskCompletionSource _release = new();

    private int _holdNextLookup;

    /// <summary>Holds the next cache lookup until <see cref="ReleaseHeldLookup"/> is called.</summary>
    public void HoldNextLookup() => Volatile.Write(ref _holdNextLookup, 1);

    /// <summary>Completes once a request has reached the held lookup.</summary>
    public Task LookupReached => _lookupReached.Task;

    /// <summary>Lets the held request continue.</summary>
    public void ReleaseHeldLookup() => _release.SetResult();

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _holdNextLookup, 0) == 1)
        {
            _lookupReached.SetResult();
            await _release.Task;
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
