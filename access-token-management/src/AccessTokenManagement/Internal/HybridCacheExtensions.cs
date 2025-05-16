// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.AccessTokenManagement.Internal;

/// <summary>
/// This extension method is created because we don't yet have a 'GetOrDefault' method
/// on HybridCache. This is under consideration:
///
/// https://github.com/dotnet/extensions/issues/5688#issuecomment-2692247434
/// </summary>
internal static class HybridCacheExtensions
{
    private static readonly HybridCacheEntryOptions GetOnlyEntryOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
                | HybridCacheEntryFlags.DisableDistributedCacheWrite
                | HybridCacheEntryFlags.DisableUnderlyingData
    };

    /// <summary>
    /// Using a get-only entry, this method will return the value if it exists in the cache.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="cache"></param>
    /// <param name="key"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    internal static async ValueTask<T?> GetOrDefaultAsync<T>(this HybridCache cache, string key, CT ct = default) =>
        await cache.GetOrCreateAsync<T?>(
            key,
            null!, // Don't return a value if it's not in the cache. Also, don't write it to the cache
            GetOnlyEntryOptions,
            cancellationToken: ct);
}
