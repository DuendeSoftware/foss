// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement.Implementation;

/// <summary>
/// DPoP nonce store using IDistributedCache
/// </summary>
internal class HybridDPoPNonceStore(
    [FromKeyedServices(ServiceProviderKeys.DPoPNonceStore)] HybridCache cache,
    IDPoPNonceStoreKeyGenerator dPoPNonceStoreKeyGenerator,
    ILogger<HybridDPoPNonceStore> logger) : IDPoPNonceStore
{
    /// <inheritdoc/>
    public async Task<string?> GetNonceAsync(DPoPNonceContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cacheKey = dPoPNonceStoreKeyGenerator.GenerateKey(context);
        var entry = await cache.GetOrDefaultAsync<string>(cacheKey, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (entry != null)
        {
            logger.CacheHitForDPoPNonce(context.Url, context.Method);
            return entry;
        }

        logger.CacheMissForDPoPNonce(context.Url, context.Method);
        return null;
    }

    /// <inheritdoc/>
    public async Task StoreNonceAsync(DPoPNonceContext context, string nonce, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var cacheExpiration = TimeSpan.FromHours(1);
        var data = nonce;

        var entryOptions = new HybridCacheEntryOptions()
        {
            Expiration = cacheExpiration
        };

        logger.WritingNonceToCache(context.Url, context.Method, cacheExpiration);

        var cacheKey = dPoPNonceStoreKeyGenerator.GenerateKey(context);
        await cache.SetAsync(cacheKey, data, entryOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
