// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Util;

public class TestServerFixture : IAsyncDisposable
{
    private readonly WebApplication _app;

    private TestServerFixture(WebApplication app, Uri address)
    {
        _app = app;
        Address = address;
    }

    public Uri Address { get; }

    public IServiceProvider Services => _app.Services;

    public static async Task<TestServerFixture> Start(
        Action<OAuth2IntrospectionOptions> options,
        DelegatingHandler? backChannelHandler = null,
        bool addCaching = false,
        CancellationToken ct = default)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls("https://127.0.0.1:0");

        if (addCaching)
        {
            builder.Services.AddDistributedMemoryCache();
        }
        builder.Services
            .AddAuthentication(OAuth2IntrospectionDefaults.AuthenticationScheme)
            .AddOAuth2Introspection(options);

        if (backChannelHandler != null)
        {
            builder.Services
                .AddHttpClient(OAuth2IntrospectionDefaults.BackChannelHttpClientName)
                .AddHttpMessageHandler(() => backChannelHandler);
        }

        var app = builder.Build();

        app.UseAuthentication();

        app.Run(async context =>
        {
            var user = context.User;

            if (user.Identity!.IsAuthenticated)
            {
                var token = await context.GetTokenAsync("access_token");
                var responseObject = new Dictionary<string, string>
                {
                    {"token", token! }
                };
                var json = JsonSerializer.Serialize(responseObject);
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(json, Encoding.UTF8, context.RequestAborted);
            }
            else
            {
                context.Response.StatusCode = 401;
            }
        });

        await app.StartAsync(ct);

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();
        var addressUri = new Uri(address);

        return new TestServerFixture(app, addressUri);
    }

    public HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        var client = new HttpClient(handler)
        {
            BaseAddress = Address
        };
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
