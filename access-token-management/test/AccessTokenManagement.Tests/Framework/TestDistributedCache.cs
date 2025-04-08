// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;

namespace Duende.AccessTokenManagement.Tests;

internal class TestDistributedCache : IDistributedCache
{
    private readonly Dictionary<string, byte[]?> _cache = new();

    public byte[]? Get(string key) => _cache[key];

    public Task<byte[]?> GetAsync(string key, CancellationToken token = new CancellationToken()) => Task.FromResult(_cache[key]);

    public void Refresh(string key)
    {
    }

    public Task RefreshAsync(string key, CancellationToken token = new CancellationToken())
    {
        Refresh(key);
        return Task.CompletedTask;
    }

    public void Remove(string key) => _cache.Remove(key);

    public Task RemoveAsync(string key, CancellationToken token = new CancellationToken())
    {
        Remove(key);
        return Task.CompletedTask;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _cache.Add(key, value);

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
        CancellationToken token = new CancellationToken())
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }
}
