// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Duende.AccessTokenManagement.AccessTokenHandlers.Fixtures;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.Framework;
using Duende.AccessTokenManagement.Tests;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AccessTokenManagement.AccessTokenHandler.Fixtures;

internal class ClientCredentialsFixtureWithAutotuning : AccessTokenHandlingBaseFixture
{
    public TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    public override ValueTask InitializeAsync(DPoPProofKey? dPoPJsonWebKey)
    {
        Services.AddDistributedMemoryCache();
        Services.AddSingleton<IDistributedCache>(new FakeDistributedCache(new FakeTimeProvider(() => The.CurrentDate)));
        Services.AddClientCredentialsTokenManagement(options =>
            {
                options.UseCacheAutoTuning = true;

                // explicitly set the local cache expiration very low. this makes sure the remote cache is used. 
                options.LocalCacheExpiration = TimeSpan.FromMilliseconds(10);
                options.DefaultCacheLifetime = CacheExpiration;
            })
            .AddClient("tokenClient", opt =>
            {
                opt.TokenEndpoint = TokenEndpoint.TokenEndpoint;
                opt.ClientId = ClientId.Parse("clientId");
                opt.ClientSecret = ClientSecret.Parse("clientSecret");
                opt.HttpClientName = "tokenHttpClient";
                opt.DPoPJsonWebKey = dPoPJsonWebKey;
            });
        Services.AddClientCredentialsHttpClient("httpClient", ClientCredentialsClientName.Parse("tokenClient"))
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = ApiEndpoint.Uri;
            })
            .ConfigurePrimaryHttpMessageHandler(() => ApiEndpoint);

        return ValueTask.CompletedTask;
    }
}
