// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AspNetCore.Authentication.OAuth2Introspection.Util;
using Duende.IdentityModel.Client;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection;

public class Configuration
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Empty_Options()
    {
        var client = await PipelineFactory.CreateClient(_ => { });

        var act = async () => await client.GetAsync("http://test", _ct);

        var result = await act.ShouldThrowAsync<InvalidOperationException>();
        result.Message.ShouldBe("You must either set Authority or IntrospectionEndpoint");
    }

    [Fact]
    public async Task Endpoint_But_No_Authority()
    {
        var client = await PipelineFactory.CreateClient(options =>
        {
            options.IntrospectionEndpoint = "http://endpoint";
            options.ClientId = "scope";

        });

        var act = async () => await client.GetAsync("http://test", _ct);

        await act.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task No_ClientName_But_Introspection_Handler()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active);
        var client = await PipelineFactory.CreateClient(options =>
        {
            options.IntrospectionEndpoint = "http://endpoint";
        }, handler);

        var act = async () => await client.GetAsync("http://test", _ct);

        await act.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task Authority_No_Network_Delay_Load()
    {
        var client = await PipelineFactory.CreateClient(options =>
        {
            options.Authority = "http://localhost:6666";
            options.ClientId = "scope";
        });

        var act = async () => await client.GetAsync("http://test", _ct);

        await act.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task Authority_Get_Introspection_Endpoint()
    {
        OAuth2IntrospectionOptions ops = null!;
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active);

        var client = await PipelineFactory.CreateClient(options =>
        {
            options.Authority = "https://authority.com/";
            options.ClientId = "scope";

            options.DiscoveryPolicy.RequireKeySet = false;
            ops = options;
        }, handler);

        client.SetBearerToken("token");
        await client.GetAsync("http://server/api", _ct);

        ops.IntrospectionEndpoint.ShouldBe("https://authority.com/introspection_endpoint");
    }
}
