// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityModel.OidcClient.DPoP.Framework;

public class GenericHost : IAsyncDisposable
{
    public GenericHost(string baseAddress = "https://server")
    {
        if (baseAddress.EndsWith("/")) baseAddress = baseAddress.Substring(0, baseAddress.Length - 1);
        _baseAddress = baseAddress;
    }

    protected readonly string _baseAddress;
    IServiceProvider _appServices = default!;

    public Assembly HostAssembly { get; set; } = default!;
    public bool IsDevelopment { get; set; } = default!;

    public TestServer Server { get; private set; } = default!;
    public HttpClient HttpClient { get; set; } = default!;

    public TestLoggerProvider Logger { get; set; } = new TestLoggerProvider();

    public T Resolve<T>() where T : notnull =>
        // not calling dispose on scope on purpose
        _appServices.GetRequiredService<IServiceScopeFactory>().CreateScope().ServiceProvider.GetRequiredService<T>();

    public string Url(string path = null)
    {
        path = path ?? string.Empty;
        if (!path.StartsWith("/")) path = "/" + path;
        return _baseAddress + path;
    }

    public async Task InitializeAsync()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(builder =>
            {
                builder.UseTestServer();

                builder.ConfigureAppConfiguration((context, b) =>
                {
                    if (HostAssembly is not null)
                    {
                        context.HostingEnvironment.ApplicationName = HostAssembly.GetName().Name;
                    }
                });

                if (IsDevelopment)
                {
                    builder.UseSetting("Environment", "Development");
                }
                else
                {
                    builder.UseSetting("Environment", "Production");
                }

                builder.ConfigureServices(ConfigureServices);
                builder.Configure(ConfigureApp);
            });

        // Build and start the IHost
        var host = await hostBuilder.StartAsync();

        Server = host.GetTestServer();
        HttpClient = Server.CreateClient();
    }

    public event Action<IServiceCollection> OnConfigureServices = services => { };
    public event Action<IApplicationBuilder> OnConfigure = app => { };

    void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(options =>
        {
            options.SetMinimumLevel(LogLevel.Debug);
            options.AddProvider(Logger);
        });

        OnConfigureServices(services);
    }

    void ConfigureApp(IApplicationBuilder app)
    {
        _appServices = app.ApplicationServices;

        OnConfigure(app);
    }

    public async ValueTask DisposeAsync()
    {
        if (Server != null) await CastAndDispose(Server);
        if (HttpClient != null) await CastAndDispose(HttpClient);
        if (Logger != null) await CastAndDispose(Logger);

        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
                await resourceAsyncDisposable.DisposeAsync();
            else
                resource?.Dispose();
        }
    }
}
