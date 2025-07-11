// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duende.IdentityModel;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Infrastructure;

internal static class CacheExtensions
{
    private static readonly JsonSerializerOptions Options;

    static CacheExtensions()
    {
        Options = new JsonSerializerOptions
        {
            IgnoreReadOnlyFields = true,
            IgnoreReadOnlyProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        Options.Converters.Add(new ClaimConverter());
    }

    [RequiresUnreferencedCode(
        "Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(ReadOnlySpan<Byte>, JsonSerializerOptions)")]
    public static async Task<IEnumerable<Claim>> GetClaimsAsync(this IDistributedCache cache,
        OAuth2IntrospectionOptions options, string token)
    {
        var cacheKey = options.CacheKeyGenerator(options, token);
        var bytes = await cache.GetAsync(cacheKey).ConfigureAwait(false);

        if (bytes == null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<IEnumerable<Claim>>(bytes, Options);
    }

    [RequiresUnreferencedCode(
        "Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
    public static async Task SetClaimsAsync(this IDistributedCache cache, OAuth2IntrospectionOptions options,
        string token, IEnumerable<Claim> claims, TimeSpan duration, ILogger logger)
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

        var bytes = JsonSerializer.SerializeToUtf8Bytes(claims, Options);

        Log.SettingToCache(logger, absoluteLifetime, null);
        var cacheKey = options.CacheKeyGenerator(options, token);
        await cache.SetAsync(cacheKey, bytes,
            new DistributedCacheEntryOptions { AbsoluteExpiration = absoluteLifetime }).ConfigureAwait(false);
    }
}
