// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using Duende.AspNetCore.Authentication.OAuth2Introspection.Util;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection;

public class IntrospectionAnswerIsolation
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    // The handler's in-flight introspection calls are static and shared by all tests, so each test needs its own token.
    private readonly string _token = Guid.NewGuid().ToString("N");

    [Fact]
    public async Task When_a_token_is_accepted_on_one_scheme_then_another_scheme_introspects_it_at_its_own_endpoint()
    {
        var partnerServer = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active);
        var internalServer = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Inactive);
        var server = await CreateServer(
            authentication => authentication
                .AddOAuth2Introspection("partner", IntrospectAt("partner.example"))
                .AddOAuth2Introspection("internal", IntrospectAt("internal.example")),
            new() { ["partner.example"] = partnerServer, ["internal.example"] = internalServer });

        (await Send(server, "partner")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var internalResponse = await Send(server, "internal");
        internalResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        internalServer.SentIntrospectionRequest.ShouldBeTrue();
    }

    [Fact]
    public async Task When_two_schemes_introspect_the_same_token_at_the_same_time_then_each_gets_its_own_answer()
    {
        var partnerServer = new BlockingIntrospectionEndpoint();
        var internalServer = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Inactive);
        var cache = new GatedLookupCache();
        var server = await CreateServer(
            authentication => authentication
                .AddOAuth2Introspection("partner", IntrospectAt("partner.example", cacheKeyPrefix: "partner:"))
                .AddOAuth2Introspection("internal", IntrospectAt("internal.example", cacheKeyPrefix: "internal:")),
            new() { ["partner.example"] = partnerServer, ["internal.example"] = internalServer },
            cache);

        var partnerRequest = Send(server, "partner");
        var partnerCall = await partnerServer.NextCall();

        var heldInternalRequest = cache.HoldNextLookup();
        var internalRequest = Send(server, "internal");
        await heldInternalRequest.ReleaseAndWaitUntilJoined();

        partnerCall.Succeed();

        (await partnerRequest).StatusCode.ShouldBe(HttpStatusCode.OK);
        var internalResponse = await internalRequest;
        internalResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        internalServer.SentIntrospectionRequest.ShouldBeTrue();

        var laterInternalResponse = await Send(server, "internal");
        laterInternalResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task When_two_hosts_in_one_process_introspect_the_same_token_at_the_same_time_then_each_gets_its_own_answer()
    {
        var firstServer = new BlockingIntrospectionEndpoint();
        var firstHost = await CreateServer(
            authentication => authentication.AddOAuth2Introspection("api", IntrospectAt("first.example")),
            new() { ["first.example"] = firstServer });

        var secondServer = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Inactive);
        var secondHostCache = new GatedLookupCache();
        var secondHost = await CreateServer(
            authentication => authentication.AddOAuth2Introspection("api", IntrospectAt("second.example")),
            new() { ["second.example"] = secondServer },
            secondHostCache);

        var firstRequest = Send(firstHost, "api");
        var firstCall = await firstServer.NextCall();

        var heldSecondRequest = secondHostCache.HoldNextLookup();
        var secondRequest = Send(secondHost, "api");
        await heldSecondRequest.ReleaseAndWaitUntilJoined();

        firstCall.Succeed();

        (await firstRequest).StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondResponse = await secondRequest;
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        secondServer.SentIntrospectionRequest.ShouldBeTrue();
    }

    private static Action<OAuth2IntrospectionOptions> IntrospectAt(string authorizationServer, string cacheKeyPrefix = "") => options =>
    {
        options.IntrospectionEndpoint = $"https://{authorizationServer}/introspect";
        options.ClientId = $"api-at-{authorizationServer}";
        options.ClientSecret = "secret";
        options.CacheKeyPrefix = cacheKeyPrefix;
    };

    private async Task<HttpMessageHandler> CreateServer(
        Action<AuthenticationBuilder> addSchemes,
        Dictionary<string, DelegatingHandler> authorizationServers,
        GatedLookupCache? cache = null)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    addSchemes(services.AddAuthentication());

                    services.AddHttpClient(OAuth2IntrospectionDefaults.BackChannelHttpClientName)
                        .AddHttpMessageHandler(() => new AuthorizationServers(authorizationServers));

                    if (cache != null)
                    {
                        services.AddKeyedSingleton<HybridCache>(ServiceProviderKeys.IntrospectionCache, (sp, _) =>
                        {
                            cache.Inner = sp.GetRequiredService<HybridCache>();
                            return cache;
                        });
                    }
                })
                .Configure(app => app.Run(async context =>
                {
                    var authentication = await context.AuthenticateAsync(context.Request.Query["scheme"]);
                    context.Response.StatusCode = authentication.Succeeded ? 200 : 401;
                })))
            .Build();

        await host.StartAsync(_ct);
        return host.GetTestServer().CreateHandler();
    }

    private Task<HttpResponseMessage> Send(HttpMessageHandler server, string scheme)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"http://test/?scheme={Uri.EscapeDataString(scheme)}");
        request.SetBearerToken(_token);
        return new HttpClient(server).SendAsync(request, _ct).WaitAsync(TimeSpan.FromSeconds(10), _ct);
    }

    private sealed class AuthorizationServers(Dictionary<string, DelegatingHandler> serversByHost) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            new HttpMessageInvoker(serversByHost[request.RequestUri!.Host], disposeHandler: false).SendAsync(request, cancellationToken);
    }
}
