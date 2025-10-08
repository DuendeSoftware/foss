// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Duende.AspNetCore.Authentication.OAuth2Introspection.Util;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection;

public class Introspection
{
    private static readonly HybridCacheEntryOptions GetOnlyEntryOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
                | HybridCacheEntryFlags.DisableDistributedCacheWrite
                | HybridCacheEntryFlags.DisableUnderlyingData
    };

    private static readonly string clientId = "client";
    private static readonly string clientSecret = "secret";

    private readonly Action<OAuth2IntrospectionOptions> _options = o =>
    {
        o.Authority = "https://authority.com";
        o.DiscoveryPolicy.RequireKeySet = false;

        o.ClientId = clientId;
        o.ClientSecret = clientSecret;
    };

    [Fact]
    public async Task Unauthorized_Client()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Unauthorized);

        var client = PipelineFactory.CreateClient(o => _options(o), handler);
        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("http://test");
        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ActiveToken()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active);

        var client = PipelineFactory.CreateClient(_options, handler);
        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("http://test");
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var request = handler.LastRequest;
        request.ShouldContainKeyAndValue("client_id", clientId);
        request.ShouldContainKeyAndValue("client_secret", clientSecret);
    }

    [Theory]
    [InlineData(IntrospectionEndpointHandler.Behavior.Active, HttpStatusCode.OK)]
    [InlineData(IntrospectionEndpointHandler.Behavior.Unauthorized, HttpStatusCode.Unauthorized)]
    public async Task TwoConcurrentCalls_FirstIntrospectDoesNotThrow_SecondShouldNotBeCalled(
        IntrospectionEndpointHandler.Behavior behavior,
        HttpStatusCode expectedStatusCode)
    {
        const string token = "sometoken";
        var waitForTheFirstIntrospectionToStart = new ManualResetEvent(initialState: false);
        var waitForTheSecondRequestToStart = new ManualResetEvent(initialState: false);
        var handler = new IntrospectionEndpointHandler(behavior);

        var requestCount = 0;

        var messageHandler = PipelineFactory.CreateHandler(o =>
        {
            _options(o);

            o.Events.OnSendingRequest = async context =>
            {
                requestCount += 1;

                if (requestCount == 1)
                {
                    waitForTheSecondRequestToStart.WaitOne();
                    waitForTheFirstIntrospectionToStart.Set();
                    await Task.Delay(200); // wait for second request to reach the IntrospectionDictionary
                }
            };
        }, handler);

        var client1 = new HttpClient(messageHandler);
        var request1 = Task.Run(async () =>
        {
            client1.SetBearerToken(token);
            return await client1.GetAsync("http://test");
        });

        var client2 = new HttpClient(messageHandler);
        var request2 = Task.Run(async () =>
        {
            waitForTheSecondRequestToStart.Set();
            waitForTheFirstIntrospectionToStart.WaitOne();
            client2.SetBearerToken(token);
            return await client2.GetAsync("http://test");
        });

        await Task.WhenAll(request1, request2);

        var result1 = await request1;
        result1.StatusCode.ShouldBe(expectedStatusCode);

        requestCount.ShouldBe(1);

        var result2 = await request2;
        result2.StatusCode.ShouldBe(expectedStatusCode);
    }

    [Fact]
    public async Task ActiveToken_WithTwoConcurrentCalls_FirstCancelled_SecondShouldNotBeCancelled()
    {
        const string token = "sometoken";
        var cts = new CancellationTokenSource();
        var waitForTheFirstIntrospectionToStart = new ManualResetEvent(initialState: false);
        var waitForTheSecondRequestToStart = new ManualResetEvent(initialState: false);
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active);

        var messageHandler = PipelineFactory.CreateHandler(o =>
        {
            _options(o);

            o.Events.OnSendingRequest = async context =>
            {
                waitForTheSecondRequestToStart.WaitOne();
                waitForTheFirstIntrospectionToStart.Set();
                cts.Cancel();
                await Task.Delay(200); // wait for second request to reach the IntrospectionDictionary
            };
        }, handler);

        var client1 = new HttpClient(messageHandler);
        var request1 = Task.Run(async () =>
        {
            client1.SetBearerToken(token);
            var doRequest = () => client1.GetAsync("http://test", cts.Token);
            await doRequest.ShouldThrowAsync<OperationCanceledException>();
        });

        var client2 = new HttpClient(messageHandler);
        var request2 = Task.Run(async () =>
        {
            waitForTheSecondRequestToStart.Set();
            waitForTheFirstIntrospectionToStart.WaitOne();
            client2.SetBearerToken(token);
            return await client2.GetAsync("http://test");
        });

        await Task.WhenAll(request1, request2);

        var result2 = await request2;
        result2.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(5000, "testAssertion1", "testAssertion1")]
    [InlineData(-5000, "testAssertion1", "testAssertion2")]
    public async Task ActiveToken_With_ClientAssertion(int ttl, string assertion1, string assertion2)
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active);
        var count = 0;

        var client = PipelineFactory.CreateClient(o =>
        {
            _options(o);
            o.ClientSecret = null;
            o.CacheDuration = TimeSpan.FromMilliseconds(10);

            o.Events.OnUpdateClientAssertion = e =>
            {
                count++;
                e.ClientAssertion = new ClientAssertion
                {
                    Type = "testType",
                    Value = "testAssertion" + count
                };
                e.ClientAssertionExpirationTime = DateTime.UtcNow.AddMilliseconds(ttl);

                return Task.CompletedTask;
            };
        }, handler);

        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("http://test");
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var request = handler.LastRequest;
        request.ShouldContainKeyAndValue("client_id", clientId);
        request.ShouldContainKeyAndValue("client_assertion_type", "testType");
        request.ShouldContainKeyAndValue("client_assertion", assertion1);

        await Task.Delay(20); // wait for cache to expire

        result = await client.GetAsync("http://test");
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        request = handler.LastRequest;
        request.ShouldContainKeyAndValue("client_id", clientId);
        request.ShouldContainKeyAndValue("client_assertion_type", "testType");
        request.ShouldContainKeyAndValue("client_assertion", assertion2);
    }

    [Fact]
    public async Task Active_token_with_inline_event_events_should_be_called()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active);
        bool? validatedCalled = null;
        bool? failureCalled = null;

        var client = PipelineFactory.CreateClient(o =>
        {
            _options(o);

            o.Events.OnTokenValidated = e =>
            {
                validatedCalled = true;

                return Task.CompletedTask;
            };

            o.Events.OnAuthenticationFailed = e =>
            {
                failureCalled = true;

                return Task.CompletedTask;
            };

        }, handler);

        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("http://test");

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        validatedCalled.HasValue.ShouldBeTrue();
        validatedCalled!.Value.ShouldBeTrue();
        failureCalled.ShouldBeNull();
    }

    [Fact]
    public async Task ActiveToken_With_Caching_Ttl_Longer_Than_Duration()
    {
        var introspectionCalls = 0;
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active, TimeSpan.FromHours(1));
        var client = PipelineFactory.CreateClient(o =>
        {
            _options(o);

            o.CacheDuration = TimeSpan.FromMinutes(10);
            o.Events.OnSendingRequest = e =>
            {
                introspectionCalls++;
                return Task.CompletedTask;
            };
        }, handler);

        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("http://test");
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        introspectionCalls.ShouldBe(1);

        result = await client.GetAsync("http://test");
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        introspectionCalls.ShouldBe(1);
    }

    [Fact]
    public async Task ActiveToken_With_Caching_Ttl_Shorter_Than_Duration()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active, TimeSpan.FromMinutes(5));

        var client = PipelineFactory.CreateClient(o =>
        {
            _options(o);

            o.CacheDuration = TimeSpan.FromMinutes(10);
        }, handler);

        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("http://test");
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        result = await client.GetAsync("http://test");
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InactiveToken()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Inactive);

        var client = PipelineFactory.CreateClient(o => _options(o), handler);
        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("http://test");
        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InActive_token_with_inline_event_events_should_be_called()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Inactive);
        bool? validatedCalled = null;
        bool? failureCalled = null;

        var client = PipelineFactory.CreateClient(o =>
        {
            _options(o);

            o.Events.OnTokenValidated = e =>
            {
                validatedCalled = true;

                return Task.CompletedTask;
            };

            o.Events.OnAuthenticationFailed = e =>
            {
                failureCalled = true;

                return Task.CompletedTask;
            };

        }, handler);

        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("http://test");

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        validatedCalled.ShouldBeNull();
        failureCalled.HasValue.ShouldBeTrue();
        failureCalled!.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task ActiveToken_With_SavedToken()
    {
        var expectedToken = "expected_token";
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active);

        var client = PipelineFactory.CreateClient(o =>
        {
            _options(o);

            o.SaveToken = true;
        }, handler);

        client.SetBearerToken(expectedToken);

        var response = await client.GetAsync("http://test");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var responseDataStr = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<Dictionary<string, string>>(responseDataStr);

        responseData.ShouldNotBeNull();
        responseData.ShouldContainKeyAndValue("token", expectedToken);
    }

    [Fact]
    public async Task ActiveToken_With_SavedToken_And_Caching()
    {
        var expectedToken = "expected_token";
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active, TimeSpan.FromHours(1));

        var server = PipelineFactory.CreateServer(o =>
        {
            _options(o);

            o.SaveToken = true;
            o.CacheDuration = TimeSpan.FromMinutes(10);
        }, handler);
        var client = server.CreateClient();
        client.SetBearerToken(expectedToken);

        var firstResponse = await client.GetAsync("http://test");
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondResponse = await client.GetAsync("http://test");
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var responseDataStr = await secondResponse.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<Dictionary<string, string>>(responseDataStr);

        responseData.ShouldNotBeNull();
        responseData.ShouldContainKeyAndValue("token", expectedToken);
        await AssertCacheItemExists(server, string.Empty, expectedToken);
    }

    [Fact]
    public async Task ActiveToken_With_SavedToken_And_Caching_With_Cache_Key_Prefix()
    {
        var expectedToken = "expected_token";
        var cacheKeyPrefix = "KeyPrefix";
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active, TimeSpan.FromHours(1));

        var server = PipelineFactory.CreateServer(o =>
        {
            _options(o);

            o.SaveToken = true;
            o.CacheKeyPrefix = cacheKeyPrefix;
            o.CacheDuration = TimeSpan.FromMinutes(10);
        }, handler);
        var client = server.CreateClient();
        client.SetBearerToken(expectedToken);

        var firstResponse = await client.GetAsync("http://test");
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondResponse = await client.GetAsync("http://test");
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var responseDataStr = await secondResponse.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<Dictionary<string, string>>(responseDataStr);

        responseData.ShouldNotBeNull();
        responseData.ShouldContainKeyAndValue("token", expectedToken);
        await AssertCacheItemExists(server, cacheKeyPrefix, expectedToken);
    }

    [Fact]
    public async Task Repeated_active_token_with_caching_enabled_should_hit_cache()
    {
        var expectedToken = "expected_token";
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active, TimeSpan.FromHours(1));

        var server = PipelineFactory.CreateServer(o =>
        {
            _options(o);

            o.SaveToken = true;
            o.CacheDuration = TimeSpan.FromMinutes(10);
        }, handler);
        var client = server.CreateClient();
        client.SetBearerToken(expectedToken);

        var firstResponse = await client.GetAsync("http://test");

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.SentIntrospectionRequest.ShouldBeTrue();

        handler.SentIntrospectionRequest = false;
        var secondResponse = await client.GetAsync("http://test");
        handler.SentIntrospectionRequest.ShouldBeFalse();
        await AssertCacheItemExists(server, string.Empty, expectedToken);
    }

    [Fact]
    public async Task Repeated_inactive_token_with_caching_enabled_should_hit_cache()
    {
        var expectedToken = "expected_token";
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Inactive);

        var server = PipelineFactory.CreateServer(o =>
        {
            _options(o);

            o.SaveToken = true;
            o.CacheDuration = TimeSpan.FromMinutes(10);
        }, handler);
        var client = server.CreateClient();
        client.SetBearerToken(expectedToken);

        var firstResponse = await client.GetAsync("http://test");

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        handler.SentIntrospectionRequest.ShouldBeTrue();

        handler.SentIntrospectionRequest = false;
        var secondResponse = await client.GetAsync("http://test");
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        handler.SentIntrospectionRequest.ShouldBeFalse();
        await AssertCacheItemExists(server, string.Empty, expectedToken);
    }

    [Fact]
    public async Task ActiveToken_With_Discovery_Unavailable_On_First_Request()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active);

        var client = PipelineFactory.CreateClient(o => _options(o), handler);
        client.SetBearerToken("sometoken");

        handler.IsDiscoveryFailureTest = true;
        await Should.ThrowAsync<InvalidOperationException>(async () => await client.GetAsync("http://test"));

        handler.IsDiscoveryFailureTest = false;
        var result = await client.GetAsync("http://test");
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ActiveToken_RequestSending_AdditionalParameter_with_inline_event()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active);

        var client = PipelineFactory.CreateClient(o =>
        {
            _options(o);

            o.Events.OnSendingRequest = e =>
            {
                e.TokenIntrospectionRequest.Parameters = Parameters.FromObject(new { additionalParameter = "42" })!;
                return Task.CompletedTask;
            };

        }, handler);

        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("http://test");
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        handler.LastRequest.ShouldContain(new KeyValuePair<string, string>("additionalParameter", "42"));
    }

    [Fact]
    public async Task ActiveToken_expires_while_cached()
    {
        var introspectionRequestsMade = 0;
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active, TimeSpan.FromMilliseconds(250));

        var client = PipelineFactory.CreateClient(o =>
        {
            _options(o);

            o.CacheDuration = TimeSpan.FromMinutes(10);

            o.Events.OnSendingRequest = e =>
            {
                introspectionRequestsMade++;
                return Task.CompletedTask;
            };
        }, handler);

        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("http://test");
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        introspectionRequestsMade.ShouldBe(1);

        await Task.Delay(500); // wait for token to expire

        result = await client.GetAsync("http://test");
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        introspectionRequestsMade.ShouldBe(2);
    }

    [Fact]
    public async Task ActiveToken_can_override_cache_settings_to_prevent_caching()
    {
        var token = "sometoken";
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active, TimeSpan.FromMinutes(5));

        var server = PipelineFactory.CreateServer(o =>
        {
            _options(o);

            o.CacheDuration = TimeSpan.FromMinutes(10);
            o.SetCacheEntryFlags = HybridCacheEntryFlags.DisableLocalCacheWrite |
                                   HybridCacheEntryFlags.DisableDistributedCacheWrite;
        }, handler);
        var client = server.CreateClient();

        client.SetBearerToken(token);

        var result = await client.GetAsync("http://test");
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var cache = server.Services.GetRequiredService<HybridCache>();
        var cacheItem = await cache.GetOrCreateAsync<IEnumerable<Claim>?>(token.ToSha256(), null!, GetOnlyEntryOptions);
        cacheItem.ShouldBeNull();
    }

    private async Task AssertCacheItemExists(TestServer testServer, string cacheKeyPrefix, string token)
    {
        var cache = testServer.Services.GetRequiredService<HybridCache>();

        var cacheItem = await cache.GetOrCreateAsync<IEnumerable<Claim>?>($"{cacheKeyPrefix}{token.ToSha256()}", null!, GetOnlyEntryOptions);

        cacheItem.ShouldNotBeNull();
        cacheItem!.ShouldNotBeEmpty();
    }
}
