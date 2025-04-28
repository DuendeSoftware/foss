// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Client access token cache using IDistributedCache
/// </summary>
[Obsolete(Constants.AtmPublicSurfaceInternal, UrlFormat = Constants.AtmPublicSurfaceLink)]
public class DistributedClientCredentialsTokenCache(
    [FromKeyedServices(ServiceProviderKeys.ClientCredentialsTokenCache)] IDistributedCache cache,
    IClientCredentialsCacheKeyGenerator cacheKeyGenerator,
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

        var cacheExpiration = clientCredentialsToken.Expiration.AddSeconds(-_options.CacheLifetimeBuffer);
        var data = JsonSerializer.Serialize(clientCredentialsToken, Internal.DuendeAccessTokenSerializationContext.Default.ClientCredentialsToken);

        var entryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = cacheExpiration
        };

        logger.CachingAccessToken(clientName, cacheExpiration);

#pragma warning disable CS0618 // Type or member is obsolete
        var cacheKey = GenerateCacheKey(_options, clientName, requestParameters);
#pragma warning restore CS0618 // Type or member is obsolete
        await cache.SetStringAsync(cacheKey, data, entryOptions, token: cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClientCredentialsToken> GetOrCreateAsync(
        string clientName, TokenRequestParameters requestParameters,
        Func<string, TokenRequestParameters, CancellationToken, Task<ClientCredentialsToken>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clientName);

#pragma warning disable CS0618 // Type or member is obsolete
        var cacheKey = GenerateCacheKey(_options, clientName, requestParameters);
#pragma warning restore CS0618 // Type or member is obsolete

        if (!requestParameters.ForceRenewal)
        {
            var token = await GetAsync(clientName, requestParameters, cancellationToken).ConfigureAwait(false);
            if (token != null)
            {
                return token;
            }
        }

        return await synchronization.SynchronizeAsync(cacheKey, async () =>
        {
            var token = await factory(clientName, requestParameters, cancellationToken).ConfigureAwait(false);
            if (token.IsError)
            {
                logger.WillNotCacheTokenResultWithError(clientName, token.Error);

                return token;
            }

            try
            {
                await SetAsync(clientName, token, requestParameters, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                logger.ErrorSettingTokenInCache(e, clientName);
            }

            return token;
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ClientCredentialsToken?> GetAsync(
        string clientName,
        TokenRequestParameters requestParameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clientName);

#pragma warning disable CS0618 // Type or member is obsolete
        var cacheKey = GenerateCacheKey(_options, clientName, requestParameters);
#pragma warning restore CS0618 // Type or member is obsolete
        string? entry;

        try
        {
            entry = await cache.GetStringAsync(cacheKey, token: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.FailedToObtainTokenFromCache(ex, clientName, cacheKey);
            return null;
        }

        if (entry == null)
        {
            logger.CacheMissWhileRetrievingAccessToken(clientName);
            return null;
        }

        try
        {
            logger.CacheHitForObtainingAccessToken(clientName);
            return JsonSerializer.Deserialize<ClientCredentialsToken>(entry, Internal.DuendeAccessTokenSerializationContext.Default.ClientCredentialsToken);
        }
        catch (Exception ex)
        {
            logger.FailedToCacheAccessToken(ex, clientName);
            return null;
        }
    }

    /// <inheritdoc/>
    public Task DeleteAsync(
        string clientName,
        TokenRequestParameters requestParameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clientName);

#pragma warning disable CS0618 // Type or member is obsolete
        var cacheKey = GenerateCacheKey(_options, clientName, requestParameters);
#pragma warning restore CS0618 // Type or member is obsolete
        return cache.RemoveAsync(cacheKey, cancellationToken);
    }

    /// <summary>
    /// Generates the cache key based on various inputs
    /// </summary>
    /// <param name="options"></param>
    /// <param name="clientName"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    [Obsolete("This method is deprecated and will be removed in a future version. To customize CacheKeyGeneration, please use the property ClientCredentialsTokenManagementOptions.GenerateCacheKey")]
    protected virtual string GenerateCacheKey(
        ClientCredentialsTokenManagementOptions options,
        string clientName,
        TokenRequestParameters? parameters = null) => cacheKeyGenerator.GenerateKey(clientName, parameters);
}
