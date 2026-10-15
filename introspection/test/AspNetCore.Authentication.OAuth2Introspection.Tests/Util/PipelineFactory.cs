// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Util;

internal static class PipelineFactory
{
    public static async Task<TestServer> CreateServer(
        Action<OAuth2IntrospectionOptions> options,
        DelegatingHandler? backChannelHandler = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder => webHostBuilder
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services
                        .AddAuthentication(OAuth2IntrospectionDefaults.AuthenticationScheme)
                        .AddOAuth2Introspection(options);

                    if (backChannelHandler != null)
                    {
                        services.AddHttpClient(OAuth2IntrospectionDefaults.BackChannelHttpClientName)
                            .AddHttpMessageHandler(() => backChannelHandler);
                    }

                    configureServices?.Invoke(services);
                })
                .Configure(app =>
                {
                    app.UseAuthentication();

                    app.Run(async context =>
                    {
                        var user = context.User;

                        if (user.Identity!.IsAuthenticated)
                        {
                            var token = await context.GetTokenAsync("access_token");
                            var responseObject = new Dictionary<string, string>
                            {
                                { "token", token! }
                            };

                            var json = JsonSerializer.Serialize(responseObject);

                            context.Response.StatusCode = 200;
                            await context.Response.WriteAsync(json, Encoding.UTF8);
                        }
                        else
                        {
                            context.Response.StatusCode = 401;
                        }
                    });
                })).Build();

        await host.StartAsync();

        return host.GetTestServer();
    }

    public static async Task<HttpClient> CreateClient(
        Action<OAuth2IntrospectionOptions> options,
        DelegatingHandler? handler = null)
        => (await CreateServer(options, handler)).CreateClient();

    public static async Task<HttpMessageHandler> CreateHandler(
        Action<OAuth2IntrospectionOptions> options,
        DelegatingHandler? handler = null,
        Action<IServiceCollection>? configureServices = null) => (await CreateServer(options, handler, configureServices)).CreateHandler();
}
