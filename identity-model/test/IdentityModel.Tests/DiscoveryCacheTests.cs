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
}
