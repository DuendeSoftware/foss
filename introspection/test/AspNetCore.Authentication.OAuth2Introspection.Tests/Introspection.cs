// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.AspNetCore.Authentication.OAuth2Introspection.Util;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection;

public class Introspection
{
    private static readonly string ClientId = "client";
    private static readonly string ClientSecret = "secret";

    private readonly Action<OAuth2IntrospectionOptions> _options = o =>
    {
        o.Authority = "https://authority.com";
        o.DiscoveryPolicy.RequireKeySet = false;

        o.ClientId = ClientId;
        o.ClientSecret = ClientSecret;
    };
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Unauthorized_Client()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Unauthorized);
        await using var fixture = await TestServerFixture.Start(_options, handler, ct: _ct);
        using var client = fixture.CreateClient();
        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ActiveToken()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active);
        await using var fixture = await TestServerFixture.Start(_options, handler, ct: _ct);
        using var client = fixture.CreateClient();
        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var request = handler.LastRequest;
        request.ShouldContainKeyAndValue("client_id", ClientId);
        request.ShouldContainKeyAndValue("client_secret", ClientSecret);
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
        await using var fixture = await TestServerFixture.Start(o =>
        {
            _options(o);

            o.Events.OnSendingRequest = async context =>
            {
                requestCount += 1;

                if (requestCount == 1)
                {
                    waitForTheSecondRequestToStart.WaitOne();
                    waitForTheFirstIntrospectionToStart.Set();
                    await Task.Delay(200, _ct); // wait for second request to reach the IntrospectionDictionary
                }
            };
        }, handler, ct: _ct);

        using var client1 = fixture.CreateClient();
        var request1 = Task.Run(async () =>
        {
            client1.SetBearerToken(token);
            return await client1.GetAsync("", _ct);
        }, _ct);

        using var client2 = fixture.CreateClient();
        var request2 = Task.Run(async () =>
        {
            waitForTheSecondRequestToStart.Set();
            waitForTheFirstIntrospectionToStart.WaitOne();
            client2.SetBearerToken(token);
            return await client2.GetAsync("", _ct);
        }, _ct);

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
        await using var fixture = await TestServerFixture.Start(o =>
        {
            _options(o);
            o.Events.OnSendingRequest = async _ =>
            {
                waitForTheSecondRequestToStart.WaitOne();
                waitForTheFirstIntrospectionToStart.Set();
                await cts.CancelAsync();
                await Task.Delay(200, _ct); // wait for second request to reach the IntrospectionDictionary
            };
        }, handler, ct: _ct);

        using var client1 = fixture.CreateClient();
        var request1 = Task.Run(async () =>
        {
            client1.SetBearerToken(token);
            var doRequest = () => client1.GetAsync("", cts.Token);
            await doRequest.ShouldThrowAsync<OperationCanceledException>();
        }, _ct);

        using var client2 = fixture.CreateClient();
        var request2 = Task.Run(async () =>
        {
            waitForTheSecondRequestToStart.Set();
            waitForTheFirstIntrospectionToStart.WaitOne();
            client2.SetBearerToken(token);
            return await client2.GetAsync("", _ct);
        }, _ct);

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
        await using var fixture = await TestServerFixture.Start(o =>
        {
            _options(o);
            o.ClientSecret = null;
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
        }, handler, ct: _ct);
        using var client = fixture.CreateClient();

        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var request = handler.LastRequest;
        request.ShouldContainKeyAndValue("client_id", ClientId);
        request.ShouldContainKeyAndValue("client_assertion_type", "testType");
        request.ShouldContainKeyAndValue("client_assertion", assertion1);

        result = await client.GetAsync("", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        request = handler.LastRequest;
        request.ShouldContainKeyAndValue("client_id", ClientId);
        request.ShouldContainKeyAndValue("client_assertion_type", "testType");
        request.ShouldContainKeyAndValue("client_assertion", assertion2);
    }

    [Fact]
    public async Task Active_token_with_inline_event_events_should_be_called()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active);
        bool? validatedCalled = null;
        bool? failureCalled = null;
        await using var fixture = await TestServerFixture.Start(o =>
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
        }, handler, ct: _ct);
        using var client = fixture.CreateClient();
        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        validatedCalled.HasValue.ShouldBeTrue();
        validatedCalled!.Value.ShouldBeTrue();
        failureCalled.ShouldBeNull();
    }

    [Fact]
    public async Task ActiveToken_With_Caching_Ttl_Longer_Than_Duration()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active, TimeSpan.FromHours(1));
        await using var fixture = await TestServerFixture.Start(o =>
        {
            _options(o);
            o.EnableCaching = true;
            o.CacheDuration = TimeSpan.FromMinutes(10);
        }, handler, addCaching:true, ct: _ct);
        using var client = fixture.CreateClient();

        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        result = await client.GetAsync("", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ActiveToken_With_Caching_Ttl_Shorter_Than_Duration()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active, TimeSpan.FromMinutes(5));
        await using var fixture = await TestServerFixture.Start(o =>
        {
            _options(o);
            o.EnableCaching = true;
            o.CacheDuration = TimeSpan.FromMinutes(10);
        }, handler, addCaching: true, ct: _ct);
        using var client = fixture.CreateClient();

        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        result = await client.GetAsync("", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InactiveToken()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Inactive);
        await using var fixture = await TestServerFixture.Start(_options, handler, ct: _ct);
        using var client = fixture.CreateClient();
        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InActive_token_with_inline_event_events_should_be_called()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Inactive);
        bool? validatedCalled = null;
        bool? failureCalled = null;
        await using var fixture = await TestServerFixture.Start(o =>
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
        }, handler, ct: _ct);
        using var client = fixture.CreateClient();

        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("", _ct);

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
        await using var fixture = await TestServerFixture.Start(o =>
        {
            _options(o);
            o.SaveToken = true;
        }, handler, ct: _ct);
        using var client = fixture.CreateClient();
        client.SetBearerToken(expectedToken);

        var response = await client.GetAsync("", _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var responseDataStr = await response.Content.ReadAsStringAsync(_ct);
        var responseData = JsonSerializer.Deserialize<Dictionary<string, string>>(responseDataStr);

        responseData.ShouldNotBeNull();
        responseData.ShouldContainKeyAndValue("token", expectedToken);
    }

    [Fact]
    public async Task ActiveToken_With_SavedToken_And_Caching()
    {
        var expectedToken = "expected_token";
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active, TimeSpan.FromHours(1));
        await using var fixture = await TestServerFixture.Start(o =>
        {
            _options(o);
            o.SaveToken = true;
            o.EnableCaching = true;
            o.CacheDuration = TimeSpan.FromMinutes(10);
        }, handler, addCaching:true, ct: _ct);
        using var client = fixture.CreateClient();
        client.SetBearerToken(expectedToken);

        var firstResponse = await client.GetAsync("", _ct);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondResponse = await client.GetAsync("", _ct);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var responseDataStr = await secondResponse.Content.ReadAsStringAsync(_ct);
        var responseData = JsonSerializer.Deserialize<Dictionary<string, string>>(responseDataStr);

        responseData.ShouldNotBeNull();
        responseData.ShouldContainKeyAndValue("token", expectedToken);
        AssertCacheItemExists(fixture, string.Empty, expectedToken);
    }

    [Fact]
    public async Task ActiveToken_With_SavedToken_And_Caching_With_Cache_Key_Prefix()
    {
        var expectedToken = "expected_token";
        var cacheKeyPrefix = "KeyPrefix";
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active, TimeSpan.FromHours(1));
        await using var fixture = await TestServerFixture.Start(o =>
        {
            _options(o);
            o.SaveToken = true;
            o.EnableCaching = true;
            o.CacheKeyPrefix = cacheKeyPrefix;
            o.CacheDuration = TimeSpan.FromMinutes(10);
        }, handler, addCaching: true, ct: _ct);
        using var client = fixture.CreateClient();
        client.SetBearerToken(expectedToken);

        var firstResponse = await client.GetAsync("", _ct);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondResponse = await client.GetAsync("", _ct);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var responseDataStr = await secondResponse.Content.ReadAsStringAsync(_ct);
        var responseData = JsonSerializer.Deserialize<Dictionary<string, string>>(responseDataStr);

        responseData.ShouldNotBeNull();
        responseData.ShouldContainKeyAndValue("token", expectedToken);
        AssertCacheItemExists(fixture, cacheKeyPrefix, expectedToken);
    }

    [Fact]
    public async Task Repeated_active_token_with_caching_enabled_should_hit_cache()
    {
        var expectedToken = "expected_token";
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active, TimeSpan.FromHours(1));
        await using var fixture = await TestServerFixture.Start(o =>
        {
            _options(o);
            o.SaveToken = true;
            o.EnableCaching = true;
            o.CacheDuration = TimeSpan.FromMinutes(10);
        }, handler, addCaching: true, ct: _ct);
        using var client = fixture.CreateClient();
        client.SetBearerToken(expectedToken);

        var firstResponse = await client.GetAsync("", _ct);

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.SentIntrospectionRequest.ShouldBeTrue();

        handler.SentIntrospectionRequest = false;
        _ = await client.GetAsync("", _ct);
        handler.SentIntrospectionRequest.ShouldBeFalse();
        AssertCacheItemExists(fixture, string.Empty, expectedToken);
    }

    [Fact]
    public async Task Repeated_inactive_token_with_caching_enabled_should_hit_cache()
    {
        var expectedToken = "expected_token";
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Inactive);
        await using var fixture = await TestServerFixture.Start(o =>
        {
            _options(o);
            o.SaveToken = true;
            o.EnableCaching = true;
            o.CacheDuration = TimeSpan.FromMinutes(10);
        }, handler, addCaching: true, ct: _ct);
        using var client = fixture.CreateClient();
        client.SetBearerToken(expectedToken);

        var firstResponse = await client.GetAsync("", _ct);

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        handler.SentIntrospectionRequest.ShouldBeTrue();

        handler.SentIntrospectionRequest = false;
        var secondResponse = await client.GetAsync("", _ct);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        handler.SentIntrospectionRequest.ShouldBeFalse();
        AssertCacheItemExists(fixture, string.Empty, expectedToken);
    }

    [Fact]
    public async Task ActiveToken_With_Discovery_Unavailable_On_First_Request()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active);
        await using var fixture = await TestServerFixture.Start(_options, handler, ct: _ct);
        using var client = fixture.CreateClient();

        client.SetBearerToken("sometoken");

        handler.IsDiscoveryFailureTest = true;
        await Should.ThrowAsync<InvalidOperationException>(async () => await client.GetAsync("", _ct));

        handler.IsDiscoveryFailureTest = false;
        var result = await client.GetAsync("", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ActiveToken_RequestSending_AdditionalParameter_with_inline_event()
    {
        var handler = new IntrospectionEndpointHandler(IntrospectionEndpointHandler.Behavior.Active);
        await using var fixture = await TestServerFixture.Start(o =>
        {
            _options(o);

            o.Events.OnSendingRequest = e =>
            {
                e.TokenIntrospectionRequest.Parameters = Parameters.FromObject(new { additionalParameter = "42" })!;
                return Task.CompletedTask;
            };

        }, handler, ct: _ct);
        using var client = fixture.CreateClient();

        client.SetBearerToken("sometoken");

        var result = await client.GetAsync("", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        handler.LastRequest.ShouldContain(new KeyValuePair<string, string>("additionalParameter", "42"));
    }

    private void AssertCacheItemExists(TestServerFixture fixture, string cacheKeyPrefix, string token)
    {
        var cache = fixture.Services.GetRequiredService<IDistributedCache>();
        var cacheItem = cache.GetString($"{cacheKeyPrefix}{token.ToSha256()}");
        cacheItem.ShouldNotBeNullOrEmpty();
    }
}
