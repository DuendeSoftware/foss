// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

public interface IClientCredentialsCacheDurationStore
{
    /// <summary>
    /// Gets the expiration duration for the specified client credentials cache key.
    /// </summary>
    /// <param name="cacheKey">The cache key identifying the client credentials.</param>
    /// <returns>
    /// A <see cref="TimeSpan"/> representing the duration until the cache entry expires.
    /// </returns>
    TimeSpan GetExpiration(ClientCredentialsCacheKey cacheKey);

    /// <summary>
    /// Sets the expiration for the specified client credentials cache key.
    /// </summary>
    /// <param name="cacheKey">The cache key identifying the client credentials.</param>
    /// <param name="expiration">The expiration time to set for the cache entry.</param>
    /// <returns>
    /// A <see cref="TimeSpan"/> representing the duration until the cache entry expires after setting.
    /// </returns>
    TimeSpan SetExpiration(ClientCredentialsCacheKey cacheKey, DateTimeOffset expiration);
}
