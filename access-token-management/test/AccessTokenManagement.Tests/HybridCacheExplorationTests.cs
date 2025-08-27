// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.Framework;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;

namespace Duende.AccessTokenManagement.Tests;

public class HybridCacheExplorationTests
{
    [Fact]
    public async Task Exception_is_not_written_to_cache()
    {
        var services = new ServiceCollection()
            .AddHybridCache()
            .Services;

        var cache = services.BuildServiceProvider()
            .GetRequiredService<HybridCache>();

        var count = 0;
        object item;

        try
        {
            item = await cache.GetOrCreateAsync<object>("key", (_) =>
            {
                count++;
                throw new InvalidOperationException();
            });
        }
        catch (InvalidOperationException)
        {

        }
        item = await cache.GetOrCreateAsync<object>("key", (_) =>
        {
            count++;
            return ValueTask.FromResult<object>(null!);
        });

        item.ShouldBeNull();
        count.ShouldBe(2);
    }


    // It's not obvious how hybrid cache uses timeprovider. Actually, it doesn't. It relies on memorycache
    // which uses ISystemClock: 
    // https://github.com/dotnet/extensions/issues/6646
    // https://github.com/dotnet/runtime/issues/114011
    [Fact]
    public async Task Can_mock_timeprovider_in_hybrid_cache()
    {
        var now = DateTimeOffset.Now;

        ISystemClock systemClock = new FakeTimeProvider(() => now);
        var services = new ServiceCollection()
            // Hybrid cache uses memory cache (added implicilty)
            .AddMemoryCache()

            // memory cache doesn't use TimeProvider, but the older ISystemClock
            // It's not passed implicitly. 
            .Configure<MemoryCacheOptions>(options =>
            {
                options.Clock = systemClock;
            })
            .AddHybridCache()
            .Services;

        var cache = services.BuildServiceProvider()
            .GetRequiredService<HybridCache>();

        // write an item into the cache that's cached for 5 min. 
        await cache.GetOrCreateAsync<string>("key",
            options: new HybridCacheEntryOptions()
            {
                Expiration = TimeSpan.FromMinutes(5)
            },
            factory: _ => ValueTask.FromResult("cached"));

        //when the cached item is still valid, it shouldn't invoke the factory
        var found = await cache.GetOrCreateAsync<string>("key",
            options: new HybridCacheEntryOptions()
            {
                Expiration = TimeSpan.FromMinutes(5)
            },
            factory: _ => ValueTask.FromResult("wrong"));

        found.ShouldBe("cached");

        // move time forward by 150 min - the item should be expired now
        now += TimeSpan.FromMinutes(150);
        found = await cache.GetOrCreateAsync<string>("key", options: new HybridCacheEntryOptions()
        {
            Expiration = TimeSpan.FromMinutes(5)
        },
            factory: _ => ValueTask.FromResult("updated"));
        found.ShouldBe("updated");
    }

    // Some of ATM's code explicitly relies on hybrid cache's l1 & l2 caches. To test this properly
    // I need a way to mock out l2 cache. The fake Distributed Cache can be used for this. 
    [Fact]
    public async Task Mocking_l2_cache()
    {
        var now = DateTimeOffset.Now;

        var fakeTimeProvider = new FakeTimeProvider(() => now);
        var services = new ServiceCollection()
            .AddMemoryCache()
            .AddSingleton<IDistributedCache>(new FakeDistributedCache(fakeTimeProvider))
            .Configure<MemoryCacheOptions>(options =>
            {
                options.Clock = fakeTimeProvider;
            })
            .AddHybridCache()
            .Services;

        var cache = services.BuildServiceProvider()
            .GetRequiredService<HybridCache>();

        // write an item into the cache that's cached for 5 min in l2, and 1 sec in l1
        await cache.GetOrCreateAsync<string>("key",
            options: new HybridCacheEntryOptions()
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromSeconds(1)
            },
            factory: _ => ValueTask.FromResult("cached"));

        // when the cached item is still valid, it shouldn't invoke the factory
        var found = await cache.GetOrCreateAsync<string>("key",
            options: new HybridCacheEntryOptions()
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromSeconds(1)
            },
            factory: _ => ValueTask.FromResult("wrong"));

        found.ShouldBe("cached");
        now += TimeSpan.FromMinutes(5);

        // when the cache has expired, it should be removed from the cache and the new factory
        // should be invoked. 
        found = await cache.GetOrCreateAsync<string>("key",
            options: new HybridCacheEntryOptions()
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromSeconds(1)
            },
            factory: _ => ValueTask.FromResult("updated"));

        found.ShouldBe("updated");
    }
}
