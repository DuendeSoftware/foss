// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Duende.AccessTokenManagement.OTel;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.Internal;

internal class ClientCredentialsTokenManager(
    AccessTokenManagementMetrics metrics,
    IOptions<ClientCredentialsTokenManagementOptions> options,
    [FromKeyedServices(ServiceProviderKeys.ClientCredentialsTokenCache)] HybridCache cache,
    TimeProvider time,
    IClientCredentialsTokenEndpoint client,
    IClientCredentialsCacheKeyGenerator cacheKeyGenerator,
    ILogger<ClientCredentialsTokenManager> logger
) : IClientCredentialsTokenManager
{
    // A flag that's written into the Data property of exceptions to distinguish
    // between exceptions that are thrown inside the cache and those that are thrown
    // inside the factory.
    private const string ThrownInsideFactoryExceptionKey = "Duende.AccessTokenManagement.ThrownInside";

    // We're assuming that the cache duration for access tokens will remain (relatively) stable
    // First time we acquire an access token, don't yet know how long it will be valid, so we're assuming
    // a specific period. However, after that, we'll use the actual expiration time to set the cache duration.
    private readonly ConcurrentDictionary<ClientCredentialsCacheKey, TimeSpan> _cacheDurationAutoTuning = new();

    private readonly ClientCredentialsTokenManagementOptions _options = options.Value;

    public async Task<TokenResult<ClientCredentialsToken>> GetAccessTokenAsync(
        ClientCredentialsClientName clientName,
        TokenRequestParameters? parameters = null,
        CT ct = default)
    {
        var cacheKey = cacheKeyGenerator.GenerateKey(clientName, parameters);

        parameters ??= new TokenRequestParameters();

        var cacheExpiration = _options.UseCacheAutoTuning
            ? _cacheDurationAutoTuning.GetValueOrDefault(cacheKey, _options.DefaultCacheLifetime)
            : _options.DefaultCacheLifetime;

        // On force renewal, don't read from the cache, so we always get a new token.
        var disableDistributedCacheRead = parameters.ForceTokenRenewal.Value
            ? HybridCacheEntryFlags.DisableLocalCacheRead | HybridCacheEntryFlags.DisableDistributedCacheRead
            : HybridCacheEntryFlags.None; // Even with "none", we still get cache stampede protection :)

        var entryOptions = new HybridCacheEntryOptions()
        {
            Expiration = cacheExpiration,
            LocalCacheExpiration = _options.LocalCacheExpiration,
            Flags = disableDistributedCacheRead,
        };

        ClientCredentialsToken token;
        try
        {
            token = await cache.GetOrCreateAsync(
                key: cacheKey.ToString(),
                factory: async (c) => await RequestToken(cacheKey, clientName, parameters, c),
                options: entryOptions,
                tags: [HybridCacheConstants.CacheTag, clientName.ToString()],
                cancellationToken: ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PreventCacheException ex)
        {
            // This exception is thrown if there was a failure while retrieving an access token. We
            // don't want to cache this failure, so we throw an exception to bypass the cache action.
            logger.WillNotCacheTokenResultWithError(LogLevel.Debug, clientName, ex.Failure.Error, ex.Failure.ErrorDescription);
            return ex.Failure;
        }
        catch (Exception ex) when (!ex.Data.Contains(ThrownInsideFactoryExceptionKey))
        {
            // if there was an exception in the cache, we'll just retry without the cache and hope for the best
            logger.ExceptionWhileReadingFromCache(LogLevel.Warning, ex, clientName);
            token = await RequestToken(cacheKey, clientName, parameters, ct);
        }

        // Check if token has expired. Ideally, the cache lifetime auto-tuning should prevent this,
        // but for the first request OR if the token lifetime is changed, we might end up here.
        if (!parameters.ForceTokenRenewal.Value
            && token.Expiration - TimeSpan.FromSeconds(_options.CacheLifetimeBuffer) <= time.GetUtcNow())
        {
            // retry the request, but force a renewal
            var tokenResult = await GetAccessTokenAsync(clientName, parameters with
            {
                ForceTokenRenewal = new ForceTokenRenewal(true)
            }, ct);

            if (!tokenResult.Succeeded)
            {
                return tokenResult.FailedResult;
            }

            token = tokenResult.Token;
        }

        metrics.AccessTokenUsed(token.ClientId, AccessTokenManagementMetrics.TokenRequestType.ClientCredentials);

        return token;
    }

    private async Task<ClientCredentialsToken> RequestToken(ClientCredentialsCacheKey cacheKey,
        ClientCredentialsClientName clientName, TokenRequestParameters parameters, CT ct)
    {
        TokenResult<ClientCredentialsToken> tokenResult;
        try
        {
            tokenResult = await client.RequestAccessTokenAsync(clientName, parameters, ct);
        }
        catch (Exception ex)
        {
            // If there is a problem with retrieving data, then we want to bubble this back to the client.
            // However, we want to distinguish this from exceptions that happen inside the cache itself.
            // So, any exception that happens internally gets a special flag.
            ex.Data[ThrownInsideFactoryExceptionKey] = true;
            throw;
        }

        if (!tokenResult.WasSuccessful(out var token, out var failure))
        {
            // Unfortunately, hybrid cache has no clean way to prevent failures from being cached.
            // So we have to use an exception here.
            throw new PreventCacheException(failure);
        }

        // See if we need to record how long this access token is valid, to be used the next time
        // this access token is used.
        if (_options.UseCacheAutoTuning
            && token.Expiration != DateTimeOffset.MaxValue)
        {
            // Calculate how long this access token should be valid in the cache.
            // Note, the expiration time was just calculated by adding time.GetUTcNow() to the token lifetime.
            // so for now it's safe to subtract this time from the expiration time.
            _cacheDurationAutoTuning[cacheKey] = token.Expiration
                                                 - time.GetUtcNow()
                                                 - TimeSpan.FromSeconds(_options.CacheLifetimeBuffer);

            logger.CachingAccessToken(LogLevel.Debug, clientName, token.Expiration);
        }

        return token;
    }

    public async Task DeleteAccessTokenAsync(ClientCredentialsClientName clientName, TokenRequestParameters? parameters = null,
        CT ct = default)
    {
        var cacheKey = cacheKeyGenerator.GenerateKey(clientName, parameters);

        await cache.RemoveAsync(cacheKey.ToString(), ct);
    }

    internal class PreventCacheException(FailedResult failure) : Exception
    {
        public FailedResult Failure { get; } = failure;
    }
}
