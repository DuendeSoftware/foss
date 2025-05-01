// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.Types;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AccessTokenManagement.AccessTokenHandlers.Fixtures;

internal class ClientCredentialsFixture : AccessTokenHandlingBaseFixture
{
    public override ValueTask InitializeAsync(DPoPJsonWebKey? dPoPJsonWebKey)
    {
        Services.AddClientCredentialsTokenManagement()
            .AddClient("tokenClient", opt =>
            {
                opt.TokenEndpoint = TokenEndpoint.TokenEndpoint;
                opt.ClientId = "clientId";
                opt.ClientSecret = "clientSecret";
                opt.HttpClientName = "tokenHttpClient";
                opt.DPoPJsonWebKey = dPoPJsonWebKey;
            });
        Services.AddClientCredentialsHttpClient("httpClient", "tokenClient")
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = ApiEndpoint.Uri;
            })
            .ConfigurePrimaryHttpMessageHandler(() => ApiEndpoint);

        return ValueTask.CompletedTask;
    }
}
