// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.Internal;

/// <summary>
/// The effective cache settings for a single client, after applying per-client overrides to the global options.
/// </summary>
internal readonly record struct ResolvedCacheSettings(
    bool UseCacheAutoTuning,
    TimeSpan DefaultCacheLifetime,
    int CacheLifetimeBuffer,
    TimeSpan? LocalCacheExpiration)
{
    public static ResolvedCacheSettings Resolve(
        ClientCredentialsClientCacheOptions client,
        ClientCredentialsTokenManagementOptions global) => new(
        UseCacheAutoTuning: client.UseCacheAutoTuning ?? global.UseCacheAutoTuning,
        DefaultCacheLifetime: client.DefaultCacheLifetime ?? global.DefaultCacheLifetime,
        CacheLifetimeBuffer: client.CacheLifetimeBuffer ?? global.CacheLifetimeBuffer,
        LocalCacheExpiration: client.LocalCacheExpiration ?? global.LocalCacheExpiration);
}
