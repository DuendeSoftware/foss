// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net.Http.Json;
using System.Web;
using Duende.AccessTokenManagement.Framework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AccessTokenManagement.TokenRequestCustomizer;

public class ClientCredentialsTokenRequestCustomizerTests(ITestOutputHelper output) : IntegrationTestBase(output,
    configureUserTokenManagementOptions: opt => { opt.UseChallengeSchemeScopedTokens = true; })
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private const string ClientName = "pure_client_credentials";
    private Action<IHttpClientBuilder>? CustomizeHttpClientBuilder { get; set; }
    private Action<HttpClient, UriBuilder>? CustomizeOutgoingRequest { get; set; }

    public override async ValueTask InitializeAsync()
    {
        AppHost.OnConfigureServices += services =>
        {
            services.AddClientCredentialsTokenManagement()
                .AddClient(ClientName, client =>
                {
                    client.ClientId = ClientId.Parse("client_credentials_client");
                    client.ClientSecret = ClientSecret.Parse("secret");
                    client.TokenEndpoint = new Uri("https://identityserver/connect/token");
                    client.Scope = Scope.Parse("scope1");
                    client.HttpClient = IdentityServerHost.HttpClient;
                });

            var builder = services.AddHttpClient("clientCredentialsApi")
                .ConfigureHttpClient(client => client.BaseAddress = new Uri(ApiHost.Url()))
                .ConfigurePrimaryHttpMessageHandler(() => ApiHost.HttpMessageHandler);
            CustomizeHttpClientBuilder?.Invoke(builder);
        };

        AppHost.OnConfigure += app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/exercise_client_credentials", async (IHttpClientFactory factory, HttpContext _) =>
                {
                    var httpClient = factory.CreateClient("clientCredentialsApi");
                    var requestBuilder = new UriBuilder("test");
                    CustomizeOutgoingRequest?.Invoke(httpClient, requestBuilder);
                    var response = await httpClient.GetAsync(requestBuilder.Uri);
                    return await response.Content.ReadFromJsonAsync<TokenEchoResponse>();
                });
            });
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
            .AddClientCredentialsTokenHandler(ClientCredentialsClientName.Parse(ClientName));

        await InitializeAsync();

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/exercise_client_credentials"), _ct);
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
            .AddClientCredentialsTokenHandler(customizer,
                ClientCredentialsClientName.Parse(ClientName));

        await InitializeAsync();

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/exercise_client_credentials"), _ct);
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
            .AddClientCredentialsTokenHandler(tokenRequestCustomizerFactory,
                ClientCredentialsClientName.Parse(ClientName));

        await InitializeAsync();

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/exercise_client_credentials"), _ct);
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
            .AddClientCredentialsTokenHandler(customizer,
                ClientCredentialsClientName.Parse(ClientName));

        await InitializeAsync();

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/exercise_client_credentials"), _ct);
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
            .AddClientCredentialsTokenHandler(customizer,
                ClientCredentialsClientName.Parse(ClientName));

        await InitializeAsync();

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/exercise_client_credentials"), _ct);
        response.EnsureSuccessStatusCode();
        var token = response.ParseTokenFromResponse();
        token.Claims.ShouldContain(c => c.Type == "scope" && c.Value == "scope2");
    }
}
