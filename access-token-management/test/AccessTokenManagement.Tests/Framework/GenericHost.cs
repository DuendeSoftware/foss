// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement.Framework;

public class GenericHost(WriteTestOutput writeOutput, string baseAddress = "https://server") : IAsyncDisposable
{
    protected readonly string BaseAddress = baseAddress.EndsWith("/")
        ? baseAddress.Substring(0, baseAddress.Length - 1)
        : baseAddress;

    private ClaimsPrincipal? _userToSignIn;
    private AuthenticationProperties? _propsToSignIn;

    public Assembly HostAssembly { get; set; } = null!;

    public bool IsDevelopment { get; set; } = false!;

    public TestServer Server { get; private set; } = null!;

    public TestBrowserClient BrowserClient { get; private set; } = null!;

    public HttpClient HttpClient { get; private set; } = null!;

    public HttpMessageHandler HttpMessageHandler { get; private set; } = null!;

    private TestLoggerProvider Logger { get; } = new(writeOutput, baseAddress + " - ");

    public string Url(string? path = null)
    {
        path = path ?? string.Empty;
        if (!path.StartsWith("/"))
        {
            path = "/" + path;
        }

        return BaseAddress + path;
    }

    public async Task InitializeAsync()
    {
        if (Server != null)
        {
            throw new InvalidOperationException("Already initialized");
        }

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(builder =>
            {
                builder.UseTestServer();

                builder.ConfigureAppConfiguration((context, _) =>
                {
                    if (HostAssembly is not null)
                    {
                        context.HostingEnvironment.ApplicationName = HostAssembly.GetName().Name!;
                    }
                });

                builder.UseSetting("Environment", IsDevelopment ? "Development" : "Production");
                builder.ConfigureServices(ConfigureServices);
                builder.Configure(ConfigureApp);
            });

        // Build and start the IHost
        var host = await hostBuilder.StartAsync();

        Server = host.GetTestServer();
        BrowserClient = new TestBrowserClient(Server.CreateHandler());
        HttpClient = Server.CreateClient();
        HttpMessageHandler = Server.CreateHandler();
    }

    public event Action<IServiceCollection> OnConfigureServices = _ => { };
    public event Action<IApplicationBuilder> OnConfigure = _ => { };

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(options =>
        {
            options.SetMinimumLevel(LogLevel.Debug);
            options.AddProvider(Logger);
        });

        OnConfigureServices(services);
    }

    private void ConfigureApp(IApplicationBuilder app)
    {
        OnConfigure(app);

        ConfigureSignin(app);
        ConfigureSignout(app);
    }

    private void ConfigureSignout(IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path == "/__signout")
            {
                await ctx.SignOutAsync();
                ctx.Response.StatusCode = 204;
                return;
            }

            await next();
        });

    private void ConfigureSignin(IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path == "/__signin")
            {
                if (_userToSignIn is null)
                {
                    throw new Exception("No User Configured for SignIn");
                }

                var props = _propsToSignIn ?? new AuthenticationProperties();
                await ctx.SignInAsync(_userToSignIn, props);

                _userToSignIn = null;
                _propsToSignIn = null;

                ctx.Response.StatusCode = 204;
                return;
            }

            await next();
        });

    public async Task IssueSessionCookieAsync(params Claim[] claims)
    {
        _userToSignIn = new ClaimsPrincipal(new ClaimsIdentity(claims, "test", "name", "role"));
        var response = await BrowserClient.GetAsync(Url("__signin"));
        response.StatusCode.ShouldBe((HttpStatusCode)204);
    }

    public Task IssueSessionCookieAsync(AuthenticationProperties props, params Claim[] claims)
    {
        _propsToSignIn = props;
        return IssueSessionCookieAsync(claims);
    }

    public async ValueTask DisposeAsync()
    {
        await CastAndDispose(Server);
        await CastAndDispose(BrowserClient);
        await CastAndDispose(HttpClient);
        await CastAndDispose(HttpMessageHandler);
        await CastAndDispose(Logger);

        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
            {
                await resourceAsyncDisposable.DisposeAsync();
            }
            else
            {
                resource?.Dispose();
            }
        }
    }
}
