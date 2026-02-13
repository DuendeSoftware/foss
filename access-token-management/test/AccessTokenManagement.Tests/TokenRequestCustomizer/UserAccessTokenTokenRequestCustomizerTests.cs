// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Web;
using Duende.AccessTokenManagement.Framework;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AccessTokenManagement.TokenRequestCustomizer;

public class UserAccessTokenRequestCustomizerTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private Action<IHttpClientBuilder>? CustomizeHttpClientBuilder { get; set; }
    private Action<HttpClient, UriBuilder>? CustomizeOutgoingRequest { get; set; }

    public override async ValueTask InitializeAsync()
    {
        AppHost.OnConfigureServices += services =>
        {
            var builder = services.AddHttpClient("callApi")
                .ConfigureHttpClient(client =>
                {
                    var requestBuilder = new UriBuilder(ApiHost.Url());
                    CustomizeOutgoingRequest?.Invoke(client, requestBuilder);
                    client.BaseAddress = requestBuilder.Uri;
                })
                .ConfigurePrimaryHttpMessageHandler(() => ApiHost.HttpMessageHandler);
            CustomizeHttpClientBuilder?.Invoke(builder);
        };

        await base.InitializeAsync();
    }

    [Fact]
    public async Task Can_use_default_token_request_customizer()
    {
        var customizer = new TestTokenRequestCustomizer(_ => new TokenRequestParameters
        {
            Scope = Scope.Parse("scope2")
        });

        AppHost.OnConfigureServices += services => { services.AddSingleton<ITokenRequestCustomizer>(customizer); };

        CustomizeHttpClientBuilder = builder => builder
            .AddUserAccessTokenHandler(new UserTokenRequestParameters
            {
                Scope = Scope.Parse("scope1")
            });

        await InitializeAsync();

        await AppHost.LoginAsync("alice");

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/call_api"), _ct);
        response.EnsureSuccessStatusCode();
        var token = response.ParseTokenFromResponse();
        token.Claims.ShouldContain(c => c.Type == "scope" && c.Value == "scope2");
    }

    [Fact]
    public async Task Can_use_token_request_customizer()
    {
        var customizer = new TestTokenRequestCustomizer(_ => new TokenRequestParameters
        {
            Scope = Scope.Parse("scope2")
        });

        CustomizeHttpClientBuilder = builder => builder
            .AddUserAccessTokenHandler(customizer, new UserTokenRequestParameters
            {
                Scope = Scope.Parse("scope1")
            });

        await InitializeAsync();

        await AppHost.LoginAsync("alice");

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/call_api"), _ct);
        response.EnsureSuccessStatusCode();
        var token = response.ParseTokenFromResponse();
        token.Claims.ShouldContain(c => c.Type == "scope" && c.Value == "scope2");
    }

    [Fact]
    public async Task Can_use_token_request_customizer_factory()
    {
        Func<IServiceProvider, ITokenRequestCustomizer> tokenRequestCustomizerFactory = serviceProvider =>
            serviceProvider.GetRequiredService<ITokenRequestCustomizer>();

        var tokenRequestCustomizer = new TestTokenRequestCustomizer(_ => new TokenRequestParameters
        {
            Scope = Scope.Parse("scope2")
        });

        AppHost.OnConfigureServices += services =>
        {
            services.AddSingleton<ITokenRequestCustomizer>(tokenRequestCustomizer);
        };

        CustomizeHttpClientBuilder = builder => builder
            .AddUserAccessTokenHandler(tokenRequestCustomizerFactory, new UserTokenRequestParameters
            {
                Scope = Scope.Parse("scope1")
            });

        await InitializeAsync();

        await AppHost.LoginAsync("alice");

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/call_api"), _ct);
        response.EnsureSuccessStatusCode();
        var token = response.ParseTokenFromResponse();
        token.Claims.ShouldContain(c => c.Type == "scope" && c.Value == "scope2");
    }

    [Fact]
    public async Task Customizer_can_use_http_request_header()
    {
        var customizer = new TestTokenRequestCustomizer(request =>
        {
            var scopeHeaderValue = request.Headers.FirstOrDefault(x => x.Key == "X-Scope").Value.Single();

            return new TokenRequestParameters
            {
                Scope = Scope.Parse(scopeHeaderValue)
            };
        });

        CustomizeOutgoingRequest = (client, _) => { client.DefaultRequestHeaders.Add("X-Scope", "scope2"); };

        CustomizeHttpClientBuilder = builder => builder
            .AddUserAccessTokenHandler(customizer, new UserTokenRequestParameters
            {
                Scope = Scope.Parse("scope1")
            });

        await InitializeAsync();

        await AppHost.LoginAsync("alice");

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/call_api"), _ct);
        response.EnsureSuccessStatusCode();
        var token = response.ParseTokenFromResponse();
        token.Claims.ShouldContain(c => c.Type == "scope" && c.Value == "scope2");
    }

    [Fact]
    public async Task Customizer_can_use_http_request_uri()
    {
        var customizer = new TestTokenRequestCustomizer(request =>
        {
            var scopeInQuery = HttpUtility.ParseQueryString(request.RequestUri!.Query)["scope"];

            return new TokenRequestParameters
            {
                Scope = Scope.Parse(scopeInQuery!)
            };
        });

        CustomizeOutgoingRequest = (_, requestBuilder) => { requestBuilder.Query = "?scope=scope2"; };

        CustomizeHttpClientBuilder = builder => builder
            .AddUserAccessTokenHandler(customizer, new UserTokenRequestParameters
            {
                Scope = Scope.Parse("scope1")
            });

        await InitializeAsync();

        await AppHost.LoginAsync("alice");

        var response = await AppHost.BrowserClient.GetAsync(new Uri(new Uri(AppHost.Url()), "/call_api?scope=scope2"), _ct);
        response.EnsureSuccessStatusCode();
        var token = response.ParseTokenFromResponse();
        token.Claims.ShouldContain(c => c.Type == "scope" && c.Value == "scope2");
    }
}
