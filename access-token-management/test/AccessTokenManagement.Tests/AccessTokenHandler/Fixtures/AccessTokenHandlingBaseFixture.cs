// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.AccessTokenHandler.Helpers;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.Framework;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement.AccessTokenHandler.Fixtures;

internal abstract class AccessTokenHandlingBaseFixture : IAsyncDisposable
{
    public TestData The { get; } = new();

    public TestDataBuilder Some => new(The);

    public readonly ApiHttpMessageHandler ApiEndpoint = new();

    public readonly TokenHttpMessageHandler TokenEndpoint = new();
    public TimeSpan TokenExpiration = TimeSpan.FromSeconds(3600);

    public ServiceCollection Services = new ServiceCollection();

    public ServiceProvider ServiceProvider { get; private set; } = null!;

    public HttpClient HttpClient { get; private set; } = null!;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public abstract ValueTask InitializeAsync(DPoPProofKey? dPoPJsonWebKey);

    public async ValueTask InitializeAsync(ITestOutputHelper output, DPoPProofKey? dPoPJsonWebKey)
    {
        ApiEndpoint.DefaultRespondOkWithToken();
        TokenEndpoint.DefaultRespondWithAccessToken((int)TokenExpiration.TotalSeconds);

        Services.Configure<MemoryCacheOptions>(options =>
        {
            options.Clock = new MemoryCacheSystemClock(The.TimeProvider);
        });
        Services.AddLogging(log => log.AddProvider(new TestLoggerProvider(output.Write, "test")));
        Services.AddHttpClient("tokenHttpClient")
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = TokenEndpoint.Uri;
            })
            .ConfigurePrimaryHttpMessageHandler(() => TokenEndpoint);


        await InitializeAsync(dPoPJsonWebKey);
        ServiceProvider = Services.BuildServiceProvider();

        HttpClient = ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("httpClient");
    }
}
