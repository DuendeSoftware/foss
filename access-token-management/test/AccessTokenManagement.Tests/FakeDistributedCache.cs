// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;

namespace Duende.AccessTokenManagement.Tests;

/// <summary>
/// Implementation of a IDistributedCache for testing purposes that supports absolute and sliding expiration.
/// </summary>
/// <param name="timeProvider"></param>
public class FakeDistributedCache(TimeProvider timeProvider) : IDistributedCache
{
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly ConcurrentDictionary<string, CacheItem> _store = new();

    private class CacheItem
    {
        public byte[] Value { get; }
        public DateTimeOffset? AbsoluteExpiration { get; }
        public TimeSpan? SlidingExpiration { get; }
        public DateTimeOffset LastAccess { get; set; }

        public CacheItem(byte[] value, DateTimeOffset? absoluteExpiration, TimeSpan? slidingExpiration, DateTimeOffset now)
        {
            Value = value;
            AbsoluteExpiration = absoluteExpiration;
            SlidingExpiration = slidingExpiration;
            LastAccess = now;
        }

        public bool IsExpired(DateTimeOffset now)
        {
            if (AbsoluteExpiration.HasValue && now >= AbsoluteExpiration.Value)
            {
                return true;
            }

            if (SlidingExpiration.HasValue && now - LastAccess >= SlidingExpiration.Value)
            {
                return true;
            }

            return false;
        }
    }

    public byte[]? Get(string key)
    {
        var now = _timeProvider.GetUtcNow();
        if (_store.TryGetValue(key, out var item))
        {
            if (item.IsExpired(now))
            {
                _store.TryRemove(key, out _);
                return null;
            }
            if (item.SlidingExpiration.HasValue)
            {
                item.LastAccess = now;
            }

            return item.Value;
        }
        return null;
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        var now = _timeProvider.GetUtcNow();
        DateTimeOffset? absoluteExpiration = null;
        TimeSpan? slidingExpiration = null;

        if (options.AbsoluteExpirationRelativeToNow.HasValue)
        {
            absoluteExpiration = now + options.AbsoluteExpirationRelativeToNow.Value;
        }
        else if (options.AbsoluteExpiration.HasValue)
        {
            absoluteExpiration = options.AbsoluteExpiration.Value;
        }

        if (options.SlidingExpiration.HasValue)
        {
            slidingExpiration = options.SlidingExpiration.Value;
        }

        var item = new CacheItem(value, absoluteExpiration, slidingExpiration, now);
        _store[key] = item;
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public void Refresh(string key)
    {
        var now = _timeProvider.GetUtcNow();
        if (_store.TryGetValue(key, out var item))
        {
            if (item.IsExpired(now))
            {
                _store.TryRemove(key, out _);
            }
            else if (item.SlidingExpiration.HasValue)
            {
                item.LastAccess = now;
            }
        }
    }

    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        Refresh(key);
        return Task.CompletedTask;
    }

    public void Remove(string key) => _store.TryRemove(key, out _);

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }
}
