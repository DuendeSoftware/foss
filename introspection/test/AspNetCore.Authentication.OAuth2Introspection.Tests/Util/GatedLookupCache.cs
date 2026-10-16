// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Util;

internal sealed class GatedLookupCache : HybridCache
{
    private HeldLookup? _next;

    public HybridCache Inner { get; set; } = null!;

    public HeldLookup HoldNextLookup()
    {
        var held = new HeldLookup();
        Volatile.Write(ref _next, held);
        return held;
    }

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var held = Interlocked.Exchange(ref _next, null);
        if (held != null)
        {
            await held.Hold();
            return default!;
        }

        return await Inner.GetOrCreateAsync(key, state, factory, options, tags, cancellationToken);
    }

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default) =>
        Inner.SetAsync(key, value, options, tags, cancellationToken);

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        Inner.RemoveAsync(key, cancellationToken);

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) =>
        Inner.RemoveByTagAsync(tag, cancellationToken);
}

internal sealed class HeldLookup
{
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Completed without RunContinuationsAsynchronously, so the held request runs on the releasing thread until
    // it awaits the running introspection call. That makes "joined" observable from outside the handler.
    private readonly TaskCompletionSource _release = new();

    public async Task ReleaseAndWaitUntilJoined()
    {
        await _reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        _release.SetResult();
    }

    internal Task Hold()
    {
        _reached.SetResult();
        return _release.Task;
    }
}
