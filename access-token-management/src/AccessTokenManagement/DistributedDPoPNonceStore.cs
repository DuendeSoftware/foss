// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AccessTokenManagement;

/// <summary>
/// DPoP nonce store using IDistributedCache
/// </summary>
public class DistributedDPoPNonceStore : IDPoPNonceStore
{
    const string CacheKeyPrefix = "DistributedDPoPNonceStore";
    const char CacheKeySeparator = ':';

    private readonly HybridCache _cache;
    private readonly ILogger<DistributedDPoPNonceStore> _logger;

    private static readonly HybridCacheEntryOptions NonceStoreCacheOptions = new HybridCacheEntryOptions()
    {
        LocalCacheExpiration = TimeSpan.FromHours(1)
    };

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="logger"></param>
    public DistributedDPoPNonceStore(
        [FromKeyedServices(ServiceProviderKeys.DistributedDPoPNonceStore)]HybridCache cache, 
        ILogger<DistributedDPoPNonceStore> logger)
    {
        _cache = cache;
        _logger = logger;
    }
        
    /// <inheritdoc/>
    public virtual async Task<string?> GetNonceAsync(DPoPNonceContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cacheKey = GenerateCacheKey(context);
        var entry = await _cache.GetOrDefaultAsync<string>(cacheKey).ConfigureAwait(false);

        if (entry != null)
        {
            _logger.LogDebug("Cache hit for DPoP nonce for URL: {url}, method: {method}", context.Url, context.Method);
            return entry;
        }

        _logger.LogTrace("Cache miss for DPoP nonce for URL: {url}, method: {method}", context.Url, context.Method);
        return null;
    }

    /// <inheritdoc/>
    public virtual async Task StoreNonceAsync(DPoPNonceContext context, string nonce, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var data = nonce;


        _logger.LogTrace("Caching DPoP nonce for URL: {url}, method: {method}. Expiration: {expiration}", context.Url, context.Method, NonceStoreCacheOptions.Expiration);

        var cacheKey = GenerateCacheKey(context);
        await _cache.SetAsync(cacheKey, data, NonceStoreCacheOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
    }


    /// <summary>
    /// Generates the cache key based on various inputs
    /// </summary>
    protected virtual string GenerateCacheKey(DPoPNonceContext context)
    {
        return $"{CacheKeyPrefix}{CacheKeySeparator}{context.Url}{CacheKeySeparator}{context.Method}";
    }
}