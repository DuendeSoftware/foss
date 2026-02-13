// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Web;
using Duende.AccessTokenManagement.Framework;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AccessTokenManagement.TokenRequestCustomizer;

public class GetUserAccessTokenAsyncTests(ITestOutputHelper outputHelper) : IntegrationTestBase(outputHelper)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private Action<HttpRequest>? CustomizeOutgoingRequest { get; set; }

    public override async ValueTask InitializeAsync()
    {
        AppHost.OnConfigure += app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/exercise_client_token", async (HttpContext context) =>
                {
                    CustomizeOutgoingRequest?.Invoke(context.Request);
                    var result = await context.GetClientAccessTokenAsync(new UserTokenRequestParameters
                    {
                        Scope = Scope.Parse("scope1")
                    });

                    return await Task.FromResult(Results.Ok(new TokenEchoResponse(
                        "client",
                        result.Token!.AccessToken.ToString())));
                });
            });
        };

        await base.InitializeAsync();
    }

    [Fact]
    public async Task GetClientAccessTokenAsync_can_use_registered_token_request_customizer()
    {
        var customizer = new TestTokenRequestCustomizer(_ => new TokenRequestParameters
        {
            Scope = Scope.Parse("scope2")
        });

        AppHost.OnConfigureServices += services => { services.AddSingleton<ITokenRequestCustomizer>(customizer); };

        await InitializeAsync();

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/exercise_client_token"), _ct);

        response.EnsureSuccessStatusCode();
        var token = response.ParseTokenFromResponse();
        token.Claims.ShouldContain(c => c.Type == "scope" && c.Value == "scope2");
    }

    [Fact]
    public async Task GetClientAccessTokenAsync_can_use_http_request_header_in_registered_token_request_customizer()
    {
        var customizer = new TestTokenRequestCustomizer(request =>
        {
            var scopeHeaderValue = request.Headers.FirstOrDefault(x => x.Key == "X-Scope").Value.Single();

            return new TokenRequestParameters
            {
                Scope = Scope.Parse(scopeHeaderValue)
            };
        });

        CustomizeOutgoingRequest = request => { request.Headers.TryAdd("X-Scope", "scope2"); };

        AppHost.OnConfigureServices += services => { services.AddSingleton<ITokenRequestCustomizer>(customizer); };

        await InitializeAsync();

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/exercise_client_token"), _ct);

        response.EnsureSuccessStatusCode();
        var token = response.ParseTokenFromResponse();
        token.Claims.ShouldContain(c => c.Type == "scope" && c.Value == "scope2");
    }

    [Fact]
    public async Task GetClientAccessTokenAsync_can_use_http_request_uri_in_registered_token_request_customizer()
    {
        var customizer = new TestTokenRequestCustomizer(request =>
        {
            var scopeInQuery = HttpUtility.ParseQueryString(request.RequestUri!.Query)["scope"];

            return new TokenRequestParameters
            {
                Scope = Scope.Parse(scopeInQuery!)
            };
        });

        CustomizeOutgoingRequest = request => { request.QueryString = new QueryString("?scope=scope2"); };

        AppHost.OnConfigureServices += services => { services.AddSingleton<ITokenRequestCustomizer>(customizer); };

        await InitializeAsync();

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/exercise_client_token"), _ct);

        response.EnsureSuccessStatusCode();
        var token = response.ParseTokenFromResponse();
        token.Claims.ShouldContain(c => c.Type == "scope" && c.Value == "scope2");
    }
}
