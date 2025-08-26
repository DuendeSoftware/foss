// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AccessTokenManagement.AccessTokenHandler.Fixtures;

internal class ClientCredentialsFixtureWithAutotuning : AccessTokenHandlingBaseFixture
{
    public override ValueTask InitializeAsync(DPoPProofKey? dPoPJsonWebKey)
    {
        Services.AddClientCredentialsTokenManagement(options =>
            {
                options.UseCacheAutoTuning = true;
                options.DefaultCacheLifetime = TimeSpan.FromMilliseconds(200);
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
