// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using Duende.AspNetCore.Authentication.OAuth2Introspection.Util;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Options;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection;

public class Configuration
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Empty_Options()
    {
        var act = async () =>
        {
            await using var fixture = await TestServerFixture.Start(_ => { }, ct: _ct);
        };

        var exception = await act.ShouldThrowAsync<OptionsValidationException>();
        exception.Message.ShouldBe("You must either set Authority or IntrospectionEndpoint");
    }

    [Fact]
    public async Task Endpoint_But_No_Authority()
    {
        var act = async () =>
        {
            await using var fixture = await TestServerFixture.Start(options =>
            {
                options.IntrospectionEndpoint = "http://endpoint";
                options.ClientId = "scope";
            }, ct: _ct);
        };

        await act.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task Caching_With_Caching_Service()
    {
        var act = async () =>
        {
            await using var fixture = await TestServerFixture.Start(options =>
            {
                options.IntrospectionEndpoint = "http://endpoint";
                options.ClientId = "scope";
                options.EnableCaching = true;
            }, addCaching: true, ct: _ct);
        };

        await act.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task Caching_Without_Caching_Service()
    {
        var act = async () =>
        {
            await using var fixture = await TestServerFixture.Start(options =>
            {
                options.IntrospectionEndpoint = "http://endpoint";
                options.ClientId = "scope";
                options.EnableCaching = true;
            }, ct: _ct);
        };
        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.Message.ShouldStartWith("Caching is enabled, but no IDistributedCache is found in the services collection");
    }

    [Fact]
    public async Task No_ClientName_But_Introspection_Handler()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active);
        await using var fixture = await TestServerFixture.Start(options =>
        {
            options.IntrospectionEndpoint = "http://endpoint";
        }, handler, ct: _ct);
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("", _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Authority_No_Network_Delay_Load()
    {
        await using var fixture = await TestServerFixture.Start(options =>
        {
            options.IntrospectionEndpoint = "http://endpoint";
            options.ClientId = "scope";
        }, ct: _ct);
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("", _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Authority_Get_Introspection_Endpoint()
    {
        OAuth2IntrospectionOptions ops = null!;
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active);
        await using var fixture = await TestServerFixture.Start(options =>
        {
            options.Authority = "https://authority.com/";
            options.ClientId = "scope";
            options.DiscoveryPolicy.RequireKeySet = false;
            ops = options;
        }, handler, ct: _ct);
        using var client = fixture.CreateClient();

        client.SetBearerToken("token");
        await client.GetAsync("", _ct);

        ops.IntrospectionEndpoint.ShouldBe("https://authority.com/introspection_endpoint");
    }
}
