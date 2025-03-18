﻿using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.AccessTokenManagement;

/// <summary>
/// This extension method is created because we don't yet have a 'GetOrDefault' method
/// on HybridCache. This is under consideration:
///
/// https://github.com/dotnet/extensions/issues/5688#issuecomment-2692247434
/// </summary>
public static class HybridCacheExtMethods
{
    private static readonly HybridCacheEntryOptions GetOnlyEntryOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite 
                | HybridCacheEntryFlags.DisableDistributedCacheWrite 
                | HybridCacheEntryFlags.DisableUnderlyingData
    };

    public static async ValueTask<T?> GetOrDefaultAsync<T>(this HybridCache cache, string key)
    {
        return await cache.GetOrCreateAsync<T?>(
            key,
            null!, // Don't return a value if it's not in the cache. Also, don't write it to the cache
            GetOnlyEntryOptions
        );
    }
}