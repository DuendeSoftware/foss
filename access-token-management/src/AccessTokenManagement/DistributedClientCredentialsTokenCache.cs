// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement;

public static class ServiceProviderKeys
{
    public const string DistributedClientCredentialsTokenCache = "DistributedClientCredentialsTokenCache";
    public const string DistributedDPoPNonceStore = "DistributedDPoPNonceStore";
}

/// <summary>
/// Client access token cache using IDistributedCache
/// </summary>
public class DistributedClientCredentialsTokenCache(
    [FromKeyedServices(ServiceProviderKeys.DistributedClientCredentialsTokenCache)]HybridCache cache,
    TimeProvider time,
    ITokenRequestSynchronization synchronization,
    IOptions<ClientCredentialsTokenManagementOptions> options,
    ILogger<DistributedClientCredentialsTokenCache> logger
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

            var cacheKey = GenerateCacheKey(_options, clientName, requestParameters);
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

    private class TokenErrorException(ClientCredentialsToken token) : Exception
    {
        public ClientCredentialsToken Token { get; } = token;
    }
    public async Task<ClientCredentialsToken> GetOrCreateAsync(
        string clientName, TokenRequestParameters requestParameters,
        Func<string, TokenRequestParameters, CancellationToken, Task<ClientCredentialsToken>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clientName);

        var cacheKey = GenerateCacheKey(_options, clientName, requestParameters);

        ClientCredentialsToken? token;
        if (!requestParameters.ForceRenewal)
        {
            // We don't need the token to be absolutely fresh, so we can get one from the cache. 
            token = await cache.GetOrDefaultAsync<ClientCredentialsToken>(
                key: cacheKey,
                cancellationToken: cancellationToken);

            if (token?.Expiration > DateTimeOffset.MinValue)
            {
                var absoluteCacheExpiration = token.Expiration.AddSeconds(-_options.CacheLifetimeBuffer);

                if (absoluteCacheExpiration > time.GetUtcNow())
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


    /// <inheritdoc/>
    public ValueTask DeleteAsync(
        string clientName,
        TokenRequestParameters requestParameters,
        CancellationToken cancellationToken = default)
    {
        if (clientName is null) throw new ArgumentNullException(nameof(clientName));

        var cacheKey = GenerateCacheKey(_options, clientName, requestParameters);
        return cache.RemoveAsync(cacheKey, cancellationToken);
    }

    /// <summary>
    /// Generates the cache key based on various inputs
    /// </summary>
    /// <param name="options"></param>
    /// <param name="clientName"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    protected virtual string GenerateCacheKey(
        ClientCredentialsTokenManagementOptions options, 
        string clientName,
        TokenRequestParameters? parameters = null)
    {
        var s = "s_" + parameters?.Scope ?? "";
        var r = "r_" + parameters?.Resource ?? "";

        return options.CacheKeyPrefix + clientName + "::" + s + "::" + r;
    }
}