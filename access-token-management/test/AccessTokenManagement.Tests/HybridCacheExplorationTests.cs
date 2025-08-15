// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AccessTokenManagement;

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
}
