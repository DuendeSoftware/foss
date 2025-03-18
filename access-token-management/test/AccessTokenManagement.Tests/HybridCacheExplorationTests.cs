using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

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

        int count = 0;
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
            return ValueTask.FromResult<object>(null);
        });

        item.ShouldBeNull();
        count.ShouldBe(2);
    }
}