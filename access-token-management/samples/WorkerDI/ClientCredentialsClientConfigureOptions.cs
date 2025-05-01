// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Options;

namespace WorkerService;

public class ClientCredentialsClientConfigureOptions : IConfigureNamedOptions<ClientCredentialsClient>
{
    private readonly DiscoveryCache _cache;

    public ClientCredentialsClientConfigureOptions(DiscoveryCache cache) => _cache = cache;

    public void Configure(ClientCredentialsClient options) => throw new System.NotImplementedException();

    public void Configure(string? name, ClientCredentialsClient options)
    {
        if (name == "demo.jwt")
        {
            var disco = _cache.GetAsync().GetAwaiter().GetResult();

            options.TokenEndpoint = new Uri(disco.TokenEndpoint ?? throw new InvalidOperationException("tokenendpoint is null"));
            options.ClientId = "m2m.short.jwt";
            options.Scope = "api";
        }
    }
}
