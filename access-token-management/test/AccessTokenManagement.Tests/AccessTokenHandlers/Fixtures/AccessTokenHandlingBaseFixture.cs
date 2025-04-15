// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.AccessTokenHandlers.Helpers;
using Duende.AccessTokenManagement.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement.AccessTokenHandlers.Fixtures;

internal abstract class AccessTokenHandlingBaseFixture : IAsyncDisposable
{
    public readonly ApiHttpMessageHandler ApiEndpoint = new ApiHttpMessageHandler();
    public readonly TokenHttpMessageHandler TokenEndpoint = new TokenHttpMessageHandler();
    protected ServiceCollection Services = null!;
    public ServiceProvider ServiceProvider { get; private set; } = null!;
    public HttpClient HttpClient { get; private set; } = null!;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public abstract ValueTask InitializeAsync(bool usePreviewFeature, string? dPoPJsonWebKey);

    public async ValueTask InitializeAsync(ITestOutputHelper output, bool usePreviewFeature, string? dPoPJsonWebKey)
    {
        ApiEndpoint.DefaultRespondOkWithToken();
        TokenEndpoint.DefaultRespondWithAccessToken();

        Services = new ServiceCollection();
        Services.AddLogging(log => log.AddProvider(new TestLoggerProvider(output.Write, "test")));
        Services.AddHttpClient("tokenHttpClient")
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = TokenEndpoint.Uri;
            })
            .ConfigurePrimaryHttpMessageHandler(() => TokenEndpoint);


        await InitializeAsync(usePreviewFeature, dPoPJsonWebKey);
        ServiceProvider = Services.BuildServiceProvider();

        HttpClient = ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("httpClient");
    }
}
