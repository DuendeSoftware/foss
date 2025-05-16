// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

/// <summary>
/// Client access token options
/// </summary>
public sealed class ClientCredentialsTokenManagementOptions
{
    /// <summary>
    /// Used to prefix the cache key
    /// </summary>
    public string CacheKeyPrefix { get; set; } = "Duende.AccessTokenManagement.Cache::";

    /// <summary>
    /// Prefix used for the nonce store key
    /// </summary>
    public string NonceStoreKeyPrefix { get; set; } = "Duende.AccessTokenManagement.DPoPNonceStore::";

    /// <summary>
    /// Value to subtract from token lifetime for the cache entry lifetime (defaults to 60 seconds)
    /// </summary>
    public int CacheLifetimeBuffer { get; set; } = 60;

    /// <summary>
    /// How long should client credentials be cached.
    ///
    /// If <see cref="UseCacheAutoTuning"/> is set to false, this value will be used as the default cache lifetime
    /// otherwise, this value will be used as the default cache lifetime for the first request of a specific token.
    /// </summary>
    public TimeSpan DefaultCacheLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to use the cache auto-tuning feature.
    /// This feature tries to use the actual expiration time of the token to set the cache entry lifetime.
    /// The first time we request an access token, we don't yet know the actual expiration time, so we'll use the
    /// <see cref="DefaultCacheLifetime"/>. 
    /// </summary>
    public bool UseCacheAutoTuning { get; set; }

    /// <summary>
    /// How long should the local cache for client credential tokens be valid for?
    ///
    /// By default,w e store them for 1 minute, but this can be overridden. After this period
    /// it will be retrieved from the remote cache.
    ///
    /// Set this value to null to use the same expiration as the remote cache.
    /// </summary>
    public TimeSpan? LocalCacheExpiration { get; set; } = TimeSpan.FromMinutes(1);
}
