// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.Internal;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement.DPoP.Internal;

/// <summary>
/// DPoP nonce store using IDistributedCache
/// </summary>
internal class HybridDPoPNonceStore(
    [FromKeyedServices(ServiceProviderKeys.DPoPNonceStore)] HybridCache cache,
    IDPoPNonceStoreKeyGenerator dPoPNonceStoreKeyGenerator,
    ILogger<HybridDPoPNonceStore> logger) : IDPoPNonceStore
{
    /// <inheritdoc/>
    public async Task<DPoPNonce?> GetNonceAsync(DPoPNonceContext context, CT ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cacheKey = dPoPNonceStoreKeyGenerator.GenerateKey(context);
        var entry = await cache.GetOrDefaultAsync<string>(cacheKey, ct: ct).ConfigureAwait(false);

        if (entry == null)
        {
            logger.CacheMissForDPoPNonce(LogLevel.Trace, context.Url, context.Method);
            return null;
        }

        if (!DPoPNonce.TryParse(entry, out var parsedNonce, out var errors))
        {
            var error = string.Join(",", errors);
            logger.CachedNonceParseFailure(LogLevel.Warning, context.Url, context.Method, entry, error);
            return null;
        }
        logger.CacheHitForDPoPNonce(LogLevel.Debug, context.Url, context.Method);
        return parsedNonce;
    }

    /// <inheritdoc/>
    public async Task StoreNonceAsync(DPoPNonceContext context, DPoPNonce nonce, CT ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var cacheExpiration = TimeSpan.FromHours(1);
        var data = nonce.ToString();

        var entryOptions = new HybridCacheEntryOptions()
        {
            Expiration = cacheExpiration
        };

        logger.WritingNonceToCache(LogLevel.Debug, context.Url, context.Method, cacheExpiration);

        var cacheKey = dPoPNonceStoreKeyGenerator.GenerateKey(context);
        await cache.SetAsync(cacheKey, data, entryOptions, cancellationToken: ct).ConfigureAwait(false);
    }
}
