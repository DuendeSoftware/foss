// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityModel.Infrastructure;


namespace Duende.IdentityModel;

public class DiscoveryCacheTests
{
    private readonly NetworkHandler _successHandler;
    private const string _authority = "https://demo.identityserver.io";

    public DiscoveryCacheTests()
    {
        var discoFileName = FileName.Create("discovery.json");
        var document = File.ReadAllText(discoFileName);

        var jwksFileName = FileName.Create("discovery_jwks.json");
        var jwks = File.ReadAllText(jwksFileName);

        _successHandler = new NetworkHandler(request =>
        {
            if (request.RequestUri.AbsoluteUri.EndsWith("jwks"))
            {
                return jwks;
            }

            return document;
        }, HttpStatusCode.OK);
    }

    [Fact]
    public async Task New_initialization_should_work()
    {
        var client = new HttpClient(_successHandler);
        var cache = new DiscoveryCache(_authority, () => client);

        var disco = await cache.GetAsync();

        disco.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task New_initialization_without_authority_should_work()
    {
        var client = new HttpClient(_successHandler) { BaseAddress = new Uri(_authority) };
        var cache = new DiscoveryCache(() => client);

        var disco = await cache.GetAsync();

        disco.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task New_initialization_with_no_authority_and_client_func_without_base_address_should_throw()
    {
        var client = new HttpClient(_successHandler);
        var cache = new DiscoveryCache(() => client);

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await cache.GetAsync());

        exception.Message.ShouldBe("DiscoveryCache cannot determine the authority. Either pass the authority in the constructor or pass httpClientFunc which returns an instance of HttpClient with a BaseAddress.");
    }
}
