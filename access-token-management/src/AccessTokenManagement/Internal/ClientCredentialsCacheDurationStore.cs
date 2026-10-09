// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.Internal;

/// <summary>
/// Store for cache duration auto-tuning state.
/// </summary>
internal sealed class ClientCredentialsCacheDurationStore(
    IOptions<ClientCredentialsTokenManagementOptions> options,
    TimeProvider time) : IClientCredentialsCacheDurationStore
{
    private readonly ClientCredentialsTokenManagementOptions _options = options.Value;
    private readonly ConcurrentDictionary<ClientCredentialsCacheKey, TimeSpan> _cacheDurations = new();

    /// <summary>
    /// Gets the cache duration for a given cache key, or returns the default value if not found.
    /// </summary>
    public TimeSpan GetExpiration(ClientCredentialsCacheKey cacheKey)
    {
        var cacheExpiration = _options.UseCacheAutoTuning
            ? _cacheDurations.GetValueOrDefault(cacheKey, _options.DefaultCacheLifetime)
            : _options.DefaultCacheLifetime;
        return cacheExpiration;
    }

    /// <summary>
    /// Sets the cache duration for a given cache key.
    /// </summary>
    public TimeSpan SetExpiration(ClientCredentialsCacheKey cacheKey, DateTimeOffset expiration)
    {
        if (!_options.UseCacheAutoTuning
            || expiration == DateTimeOffset.MaxValue)
        {
            return _options.DefaultCacheLifetime;
        }

        // Calculate how long this access token should be valid in the cache.
        // Note, the expiration time was just calculated by adding time.GetUTcNow() to the token lifetime.
        // so for now it's safe to subtract this time from the expiration time.

        var calculated = expiration
                         - time.GetUtcNow()
                         - TimeSpan.FromSeconds(_options.CacheLifetimeBuffer);

        _cacheDurations[cacheKey] = calculated;

        return calculated;
    }
}
