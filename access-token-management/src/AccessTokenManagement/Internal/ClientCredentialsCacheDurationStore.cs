// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Concurrent;

namespace Duende.AccessTokenManagement.Internal;

/// <summary>
/// Store for cache duration auto-tuning state.
/// </summary>
internal sealed class ClientCredentialsCacheDurationStore(TimeProvider time)
{
    private readonly ConcurrentDictionary<ClientCredentialsCacheKey, TimeSpan> _cacheDurations = new();

    /// <summary>
    /// Gets the cache duration for a given cache key, or returns the default value if not found.
    /// </summary>
    public TimeSpan GetExpiration(ClientCredentialsCacheKey cacheKey, ResolvedCacheSettings settings)
    {
        var cacheExpiration = settings.UseCacheAutoTuning
            ? _cacheDurations.GetValueOrDefault(cacheKey, settings.DefaultCacheLifetime)
            : settings.DefaultCacheLifetime;
        return cacheExpiration;
    }

    /// <summary>
    /// Sets the cache duration for a given cache key.
    /// </summary>
    public TimeSpan SetExpiration(ClientCredentialsCacheKey cacheKey, DateTimeOffset expiration,
        ResolvedCacheSettings settings)
    {
        if (!settings.UseCacheAutoTuning
            || expiration == DateTimeOffset.MaxValue)
        {
            return settings.DefaultCacheLifetime;
        }

        // Calculate how long this access token should be valid in the cache.
        // Note, the expiration time was just calculated by adding time.GetUTcNow() to the token lifetime.
        // so for now it's safe to subtract this time from the expiration time.

        var calculated = expiration
                         - time.GetUtcNow()
                         - TimeSpan.FromSeconds(settings.CacheLifetimeBuffer);

        _cacheDurations[cacheKey] = calculated;

        return calculated;
    }
}
