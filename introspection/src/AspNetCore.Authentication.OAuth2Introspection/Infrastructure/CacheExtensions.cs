// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityModel;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Infrastructure;

internal static class CacheExtensions
{
    private static readonly HybridCacheEntryOptions GetOnlyEntryOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
                | HybridCacheEntryFlags.DisableDistributedCacheWrite
                | HybridCacheEntryFlags.DisableUnderlyingData
    };

    /// <summary>
    /// This extension method is created because we don't yet have a 'GetOrDefault' method
    /// on HybridCache. This is under consideration:
    ///
    /// https://github.com/dotnet/extensions/issues/5688#issuecomment-2692247434
    /// </summary>
    public static async Task<IEnumerable<Claim>?> GetClaimsAsync(
        this HybridCache cache,
        OAuth2IntrospectionOptions options,
        string token)
    {
        var cacheKey = options.CacheKeyGenerator(options, token);
        return await cache.GetOrCreateAsync<IEnumerable<Claim>?>(cacheKey, null!, GetOnlyEntryOptions).ConfigureAwait(false);
    }

    public static async Task SetClaimsAsync(
        this HybridCache cache,
        OAuth2IntrospectionOptions options,
        string token,
        IEnumerable<Claim> claims,
        TimeSpan duration,
        ILogger logger)
    {
        var expClaim = claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Expiration);
        var now = DateTimeOffset.UtcNow;
        var expiration = expClaim == null
            ? now + duration
            : DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim.Value));
        Log.TokenExpiresOn(logger, expiration, null);

        if (expiration <= now)
        {
            return;
        }

        // if the lifetime of the token is shorter than the duration, use the remaining token lifetime
        DateTimeOffset absoluteLifetime;
        if (expiration <= now.Add(duration))
        {
            absoluteLifetime = expiration;
        }
        else
        {
            absoluteLifetime = now.Add(duration);
        }

        Log.SettingToCache(logger, absoluteLifetime, null);
        var cacheKey = options.CacheKeyGenerator(options, token);
        var cacheDuration = absoluteLifetime - now;
        var cacheEntryOptions = new HybridCacheEntryOptions
        {
            Expiration = cacheDuration,
            LocalCacheExpiration = cacheDuration,
            Flags = options.SetCacheEntryFlags
        };
        await cache.SetAsync(cacheKey, claims, cacheEntryOptions).ConfigureAwait(false);
    }
}
