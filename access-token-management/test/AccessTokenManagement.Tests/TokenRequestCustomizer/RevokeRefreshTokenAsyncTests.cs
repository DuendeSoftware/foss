// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Web;
using Duende.AccessTokenManagement.Framework;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AccessTokenManagement.TokenRequestCustomizer;

public class RevokeRefreshTokenAsyncTests(ITestOutputHelper outputHelper) : IntegrationTestBase(outputHelper)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private Action<HttpRequest>? CustomizeOutgoingRequest { get; set; }

    public override async ValueTask InitializeAsync()
    {
        AppHost.OnConfigure += app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/exercise_refresh_token_revocation", async (HttpContext context) =>
                {
                    CustomizeOutgoingRequest?.Invoke(context.Request);
                    await context.RevokeRefreshTokenAsync(new UserTokenRequestParameters());
                    return await Task.FromResult(Results.Ok());
                });
            });
        };

        await base.InitializeAsync();
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_can_use_registered_token_request_customizer()
    {
        var assertion = new ClientAssertion
        {
            Type = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            Value = "test_jwt_assertion_value_for_revocation"
        };

        var customizer = new TestTokenRequestCustomizer(_ =>
            new TokenRequestParameters
            {
                Assertion = assertion
            });

        AppHost.OnConfigureServices += services => { services.AddSingleton<ITokenRequestCustomizer>(customizer); };

        await InitializeAsync();
        await AppHost.LoginAsync("alice");

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/exercise_refresh_token_revocation"), _ct);

        response.EnsureSuccessStatusCode();

        IdentityServerHost.CapturedRevocationRequests.ShouldNotBeEmpty();
        var revocationRequest = IdentityServerHost.CapturedRevocationRequests.First();
        revocationRequest["client_assertion_type"].ShouldBe(assertion.Type);
        revocationRequest["client_assertion"].ShouldBe(assertion.Value);
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_can_use_http_request_header()
    {
        var expectedAssertionValue = Guid.NewGuid().ToString();
        var assertion = new ClientAssertion
        {
            Type = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            Value = expectedAssertionValue
        };

        var customizer = new TestTokenRequestCustomizer(request =>
        {
            var scopeHeaderValue = request.Headers.FirstOrDefault(x => x.Key == "X-Assertion-Value").Value.Single();
            return new TokenRequestParameters
            {
                Assertion = new ClientAssertion
                {
                    Type = assertion.Type,
                    Value = scopeHeaderValue
                }
            };
        });

        CustomizeOutgoingRequest = request => { request.Headers.TryAdd("X-Assertion-Value", expectedAssertionValue); };

        AppHost.OnConfigureServices += services => { services.AddSingleton<ITokenRequestCustomizer>(customizer); };

        await InitializeAsync();
        await AppHost.LoginAsync("alice");

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/exercise_refresh_token_revocation"), _ct);

        response.EnsureSuccessStatusCode();

        IdentityServerHost.CapturedRevocationRequests.ShouldNotBeEmpty();
        var revocationRequest = IdentityServerHost.CapturedRevocationRequests.First();
        revocationRequest["client_assertion_type"].ShouldBe(assertion.Type);
        revocationRequest["client_assertion"].ShouldBe(expectedAssertionValue);
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_can_use_http_request_uri()
    {
        var expectedAssertionValue = Guid.NewGuid().ToString();
        var assertion = new ClientAssertion
        {
            Type = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            Value = expectedAssertionValue
        };

        var customizer = new TestTokenRequestCustomizer(request =>
        {
            var assertionValue = HttpUtility.ParseQueryString(request.RequestUri!.Query)["assertionValue"];
            return new TokenRequestParameters
            {
                Assertion = new ClientAssertion
                {
                    Type = assertion.Type,
                    Value = assertionValue!
                }
            };
        });

        CustomizeOutgoingRequest = request =>
        {
            request.QueryString = new QueryString($"?assertionValue={expectedAssertionValue}");
        };

        AppHost.OnConfigureServices += services => { services.AddSingleton<ITokenRequestCustomizer>(customizer); };

        await InitializeAsync();
        await AppHost.LoginAsync("alice");

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/exercise_refresh_token_revocation"), _ct);

        response.EnsureSuccessStatusCode();

        IdentityServerHost.CapturedRevocationRequests.ShouldNotBeEmpty();
        var revocationRequest = IdentityServerHost.CapturedRevocationRequests.First();
        revocationRequest["client_assertion_type"].ShouldBe(assertion.Type);
        revocationRequest["client_assertion"].ShouldBe(expectedAssertionValue);
    }
}
