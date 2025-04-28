// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.Internal;

/// <summary>
/// Client access token cache using IDistributedCache
/// </summary>
internal class HybridClientCredentialsTokenCache(
    [FromKeyedServices(ServiceProviderKeys.ClientCredentialsTokenCache)] HybridCache cache,
    TimeProvider time,
    IClientCredentialsCacheKeyGenerator cacheKeyGenerator,
    ITokenRequestSynchronization synchronization,
    IOptions<ClientCredentialsTokenManagementOptions> options,
    ILogger<HybridClientCredentialsTokenCache> logger
    )
    : IClientCredentialsTokenCache
{
    private readonly ClientCredentialsTokenManagementOptions _options = options.Value;

    /// <inheritdoc/>
    public async Task SetAsync(
        string clientName,
        ClientCredentialsToken clientCredentialsToken,
        TokenRequestParameters requestParameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clientName);

        try
        {
            var entryOptions = GetHybridCacheEntryOptions(clientCredentialsToken);

            var cacheKey = cacheKeyGenerator.GenerateKey(clientName, requestParameters);
            logger.LogTrace("Caching access token for client: {clientName}. Expiration: {expiration}", clientName, entryOptions.Expiration);
            await cache.SetAsync(cacheKey, clientCredentialsToken, entryOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            logger.LogError(e,
                "Error trying to set token in cache for client {clientName}. Error = {error}",
                clientName, e.Message);
        }
    }

    private HybridCacheEntryOptions GetHybridCacheEntryOptions(ClientCredentialsToken clientCredentialsToken)
    {
        var absoluteCacheExpiration = clientCredentialsToken.Expiration.AddSeconds(-_options.CacheLifetimeBuffer);
        var relativeCacheExpiration = absoluteCacheExpiration - time.GetUtcNow();
        var entryOptions = new HybridCacheEntryOptions()
        {
            Expiration = relativeCacheExpiration
        };
        return entryOptions;
    }
    public async Task<ClientCredentialsToken> GetOrCreateAsync(
        string clientName, TokenRequestParameters requestParameters,
        Func<string, TokenRequestParameters, CancellationToken, Task<ClientCredentialsToken>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clientName);

        var cacheKey = cacheKeyGenerator.GenerateKey(clientName, requestParameters);

        ClientCredentialsToken? token;
        if (!requestParameters.ForceRenewal)
        {
            // We don't need the token to be absolutely fresh, so we can get one from the cache. 
            token = await cache.GetOrDefaultAsync<ClientCredentialsToken>(
                key: cacheKey,
                cancellationToken: cancellationToken);

            if (token != null)
            {
                var absoluteCacheExpiration = token.Expiration.AddSeconds(-_options.CacheLifetimeBuffer);

                if (token.Expiration > DateTimeOffset.MinValue && absoluteCacheExpiration > time.GetUtcNow())
                {
                    // It's possible that we have only read the token from L2 cache, not L1 cache. 
                    // just to be sure, write the token also into L1 cache (which should be fast)
                    // https://github.com/dotnet/extensions/issues/5688#issuecomment-2692247434
                    var defaultWriteOptions = GetHybridCacheEntryOptions(token);
                    await cache.SetAsync(cacheKey, token, new HybridCacheEntryOptions()
                    {
                        Flags = HybridCacheEntryFlags.DisableDistributedCacheWrite,
                        Expiration = defaultWriteOptions.Expiration,
                        LocalCacheExpiration = defaultWriteOptions.LocalCacheExpiration
                    }, cancellationToken: cancellationToken);

                    return token;
                }
            }
        }

        // Apparently, there's either no value in the cache, or we want a fresh one.
        // Since we aren't using GetOrCreate, we'll have to protect against cache stampedes ourselves.
        return await synchronization.SynchronizeAsync(cacheKey, async () =>
        {
            token = await factory(clientName, requestParameters, cancellationToken).ConfigureAwait(false);

            // Don't cache the token if there's an error. 
            if (!token.IsError)
            {
                await SetAsync(clientName, token, requestParameters, cancellationToken);
            }

            return token;
        });

    }
    public async Task<ClientCredentialsToken?> GetAsync(string clientName, TokenRequestParameters requestParameters,
        CancellationToken cancellationToken = default)
    {
        if (clientName is null) throw new ArgumentNullException(nameof(clientName));

        var cacheKey = cacheKeyGenerator.GenerateKey(clientName, requestParameters);

        return await cache.GetOrDefaultAsync<ClientCredentialsToken>(cacheKey, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string clientName,
        TokenRequestParameters requestParameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clientName);

        var cacheKey = cacheKeyGenerator.GenerateKey(clientName, requestParameters);
        await cache.RemoveAsync(cacheKey, cancellationToken);
    }
}
