// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement;

/// <summary>
/// DPoP nonce store using IDistributedCache
/// </summary>
[Obsolete(Constants.AtmPublicSurfaceInternal, UrlFormat = Constants.AtmPublicSurfaceLink)]
public class DistributedDPoPNonceStore(
    [FromKeyedServices(ServiceProviderKeys.DPoPNonceStore)] IDistributedCache cache,
    IDPoPNonceStoreKeyGenerator dPoPNonceStoreKeyGenerator,
    ILogger<DistributedDPoPNonceStore> logger) : IDPoPNonceStore
{
    /// <inheritdoc/>
    public virtual async Task<string?> GetNonceAsync(DPoPNonceContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

#pragma warning disable CS0618 // Type or member is obsolete
        var cacheKey = GenerateCacheKey(context);
#pragma warning restore CS0618 // Type or member is obsolete
        var entry = await cache.GetStringAsync(cacheKey, token: cancellationToken).ConfigureAwait(false);

        if (entry != null)
        {
            logger.CacheHitForDPoPNonce(context.Url, context.Method);
            return entry;
        }

        logger.CacheMissForDPoPNonce(context.Url, context.Method);
        return null;
    }

    /// <inheritdoc/>
    public virtual async Task StoreNonceAsync(DPoPNonceContext context, string nonce, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cacheExpiration = DateTimeOffset.UtcNow.AddHours(1);
        var data = nonce;

        var entryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = cacheExpiration
        };

        logger.WritingNonceToCache(context.Url, context.Method, cacheExpiration);

#pragma warning disable CS0618 // Type or member is obsolete
        var cacheKey = GenerateCacheKey(context);
#pragma warning restore CS0618 // Type or member is obsolete
        await cache.SetStringAsync(cacheKey, data, entryOptions, token: cancellationToken).ConfigureAwait(false);
    }


    /// <summary>
    /// Generates the cache key based on various inputs
    /// </summary>
    [Obsolete("This method is deprecated and will be removed in a future version. To customize CacheKeyGeneration, please use the property ClientCredentialsTokenManagementOptions.GenerateNonceStoreKey")]
    protected virtual string GenerateCacheKey(DPoPNonceContext context)
    {
        return dPoPNonceStoreKeyGenerator.GenerateKey(context);
    }
}
