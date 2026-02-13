// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.Framework;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AccessTokenManagement.TokenRequestCustomizer;

public class ScenarioTests(ITestOutputHelper outputHelper) : IntegrationTestBase(outputHelper)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task
        OpenIdConnectUserAccessTokenRetriever_and_GetClientAccessTokenExtensionMethod_can_use_the_same_token_request_customizer()
    {
        var requestPaths = new List<string>();
        var customizer = new TestTokenRequestCustomizer(request =>
        {
            requestPaths.Add(request.RequestUri?.AbsolutePath ?? "");
            return new TokenRequestParameters();
        });

        AppHost.OnConfigureServices += services =>
        {
            services.AddSingleton<ITokenRequestCustomizer>(customizer);
            services.AddHttpClient(The.ClientId)
                .AddClientAccessTokenHandler()
                .ConfigurePrimaryHttpMessageHandler(() => ApiHost.HttpMessageHandler);
        };

        AppHost.OnConfigure += app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/exercise_client_token", async (HttpContext context) =>
                {
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

        await InitializeAsync();
        await AppHost.LoginAsync("alice");

        var extensionMethodResponse =
            await AppHost.BrowserClient.GetAsync(AppHost.Url("/exercise_client_token"), _ct);
        extensionMethodResponse.EnsureSuccessStatusCode();

        var clientAccessTokenManagerResponse =
            await AppHost.BrowserClient.GetAsync(new Uri(new Uri(AppHost.Url()), "/call_api"), _ct);
        clientAccessTokenManagerResponse.EnsureSuccessStatusCode();

        requestPaths.ToArray().ShouldBeEquivalentTo(new[]
        {
            //HttpContextExtensions.GetClientAccessTokenAsync
            "/exercise_client_token",
            //OpenIdConnectUserAccessTokenRetriever
            "/test"
        });
    }
}
