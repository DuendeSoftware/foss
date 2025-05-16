// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using Duende.AccessTokenManagement.Framework;

using Duende.IdentityModel.Client;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;

namespace Duende.AccessTokenManagement.Tests;

public class BackChannelClientTests(ITestOutputHelper output)
{
    public TestData The { get; } = new TestData();
    public TestDataBuilder Some => new TestDataBuilder(The);


    [Fact]
    public async Task Will_use_cache()
    {
        var services = new ServiceCollection();

        services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                Some.ClientCredentialsClient(client);
            });

        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When(The.TokenEndpoint.ToString())
            .Respond((_) => Some.TokenHttpResponse());

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        // Getting the token twice should result in a single call (because it' cached)
        var token = await sut.GetAccessTokenAsync("test");
        token.Succeeded.ShouldBeTrue();
        token = await sut.GetAccessTokenAsync("test");
        token.Succeeded.ShouldBeTrue();

        mockHttp.GetMatchCount(request).ShouldBe(1);
    }

    [Fact]
    public async Task Can_delete_token_from_cache()
    {
        var services = new ServiceCollection();

        services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                Some.ClientCredentialsClient(client);
            });

        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When(The.TokenEndpoint.ToString())
            .Respond((_) => Some.TokenHttpResponse());

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        // Get the first token (should result in a call)
        var token = await sut.GetAccessTokenAsync("test");
        token.Succeeded.ShouldBeTrue();

        // then delete the token
        await sut.DeleteAccessTokenAsync("test");

        // Now get another token. This should result in another call
        token = await sut.GetAccessTokenAsync("test");
        token.Succeeded.ShouldBeTrue();
        mockHttp.GetMatchCount(request).ShouldBe(2);
    }

    [Fact]
    public async Task Get_access_token_uses_default_backchannel_client_from_factory()
    {
        var services = new ServiceCollection();

        services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                client.TokenEndpoint = new Uri("https://as");
                client.ClientId = "id";
                client.ClientSecret = "required";
            });

        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://as/*")
            .Respond((_) => new HttpResponseMessage(HttpStatusCode.NotFound));

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync("test");

        token.Succeeded.ShouldBeFalse();
        token.FailedResult!.Error.ShouldBe("Not Found");
        mockHttp.GetMatchCount(request).ShouldBe(1);
    }

    [Fact]
    public async Task Get_access_token_uses_custom_backchannel_client_from_factory()
    {
        var services = new ServiceCollection();

        services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                client.TokenEndpoint = new Uri("https://as");
                client.ClientId = "id";
                client.ClientSecret = "required";
                client.HttpClientName = "custom";
            });

        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://as/*")
            .Respond(HttpStatusCode.NotFound);

        services.AddHttpClient("custom")
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync("test");

        token.Succeeded.ShouldBeFalse();
        token.FailedResult!.Error.ShouldBe("Not Found");
        mockHttp.GetMatchCount(request).ShouldBe(1);
    }

    [Fact]
    public async Task Getting_a_token_with_different_scope_twice_sequentially_will_result_in_two_calls()
    {
        var services = new ServiceCollection();

        services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                client.TokenEndpoint = new Uri("https://as");
                client.ClientId = "id";
                client.ClientSecret = "required";
                client.HttpClientName = "custom";
            });

        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://as/*")
            .Respond((_) => Some.TokenHttpResponse());

        services.AddHttpClient("custom")
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        ClientCredentialsToken token1 = null!;
        token1 = await sut.GetAccessTokenAsync("test", new TokenRequestParameters()
        {
            ForceTokenRenewal = false,
            Scope = "scope1",

        }).GetToken();


        ClientCredentialsToken token2 = null!;

        token2 = await sut.GetAccessTokenAsync("test", new TokenRequestParameters()
        {
            ForceTokenRenewal = false,
            Scope = "scope2",

        }).GetToken();

        mockHttp.GetMatchCount(request).ShouldBe(2);

    }

    [Fact]
    public async Task Getting_a_token_with_different_scope_twice_concurrently_will_result_two_calls()
    {
        var services = new ServiceCollection();

        services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                client.TokenEndpoint = new Uri("https://as");
                client.ClientId = "id";
                client.ClientSecret = "required";
                client.HttpClientName = "custom";
            });

        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://as/*")
            .Respond((_) => Some.TokenHttpResponse());

        services.AddHttpClient("custom")
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        mockHttp.AutoFlush = false;

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        ClientCredentialsToken token1 = null!;
        var t1 = Task.Run(async () =>
        {
            token1 = await sut.GetAccessTokenAsync("test", new TokenRequestParameters()
            {
                ForceTokenRenewal = false,
                Scope = "scope1",

            }).GetToken();
        });
        await Task.Delay(100);


        ClientCredentialsToken token2 = null!;
        var t2 = Task.Run(async () =>
        {
            token2 = await sut.GetAccessTokenAsync("test", new TokenRequestParameters()
            {
                ForceTokenRenewal = false,
                Scope = "scope2",

            }).GetToken();
        });


        output.WriteLine("before delay");

        await Task.Delay(100);

        mockHttp.Flush();
        output.WriteLine("flushed");
        await t1.ThrowOnTimeout();
        await t2.ThrowOnTimeout();

        mockHttp.GetMatchCount(request).ShouldBe(2);

    }

    [Fact]
    public async Task Getting_a_token_with_different_parameters_twice_concurrently_will_result_two_calls()
    {
        var services = new ServiceCollection();

        services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                client.TokenEndpoint = new Uri("https://as");
                client.ClientId = "id";
                client.ClientSecret = "required";
                client.HttpClientName = "custom";
            });

        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://as/*")
            .Respond((_) => Some.TokenHttpResponse());

        services.AddHttpClient("custom")
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        mockHttp.AutoFlush = true;

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        ClientCredentialsToken token1 = null!;
        var t1 = Task.Run(async () =>
        {
            token1 = await sut.GetAccessTokenAsync("test", new TokenRequestParameters()
            {
                ForceTokenRenewal = false,
                Parameters = new Parameters()
                {
                    { "tenant", "1" }
                }

            }).GetToken();
        });
        await Task.Delay(100);


        ClientCredentialsToken token2 = null!;
        var t2 = Task.Run(async () =>
        {
            token2 = await sut.GetAccessTokenAsync("test", new TokenRequestParameters()
            {
                ForceTokenRenewal = false,
                Parameters = new Parameters()
                {
                    { "tenant", "2" }
                }

            }).GetToken();
        });


        output.WriteLine("before delay");

        await Task.Delay(100);

        mockHttp.Flush();
        output.WriteLine("flushed");
        await t1.ThrowOnTimeout();
        await t1.ThrowOnTimeout();

        mockHttp.GetMatchCount(request).ShouldBe(1);

    }

    [Fact]
    public async Task Get_access_token_uses_specific_http_client_instance()
    {
        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://as/*")
            .Respond(HttpStatusCode.NotFound);
        var mockClient = mockHttp.ToHttpClient();

        var services = new ServiceCollection();

        services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                client.TokenEndpoint = new Uri("https://as");
                client.ClientId = "id";
                client.ClientSecret = "required";
                client.HttpClient = mockClient;
            });

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync("test");

        token.Succeeded.ShouldBeFalse();
        token.FailedResult!.Error.ShouldBe("Not Found");
        mockHttp.GetMatchCount(request).ShouldBe(1);
    }

    [Fact]
    public async Task Can_use_custom_cache_implementation()
    {
        var services = new ServiceCollection();

        var replacementCache = new FakeHybridCache();
        services.AddKeyedSingleton<HybridCache>(ServiceProviderKeys.ClientCredentialsTokenCache, replacementCache);

        services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                client.TokenEndpoint = new Uri("https://as");
                client.ClientId = "id";
                client.ClientSecret = "required";
                client.HttpClientName = "custom";
            });

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://as/*")
            .Respond((_) => Some.TokenHttpResponse());

        services.AddHttpClient("custom")
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync("test").GetToken();

        // Verify we actually used the cache
        replacementCache.GetOrCreateCount.ShouldBe(1);
    }

    [Fact]
    public async Task Can_use_custom_key_generator()
    {
        var services = new ServiceCollection();

        var replacementCache = new FakeHybridCache();
        services.AddSingleton<IClientCredentialsCacheKeyGenerator>(new AlwaysSameKeyCacheKeyGenerator("always_the_same"));
        services.AddKeyedSingleton<HybridCache>(ServiceProviderKeys.ClientCredentialsTokenCache, replacementCache);

        services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                client.TokenEndpoint = new Uri("https://as");
                client.ClientId = "id";
                client.ClientSecret = "required";
                client.HttpClientName = "custom";
            });

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://as/*")
            .Respond((_) => Some.TokenHttpResponse());

        services.AddHttpClient("custom")
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync("test").GetToken();

        replacementCache.CacheKey.ShouldBe("always_the_same");

    }

    public class AlwaysSameKeyCacheKeyGenerator(string cacheKey) : IClientCredentialsCacheKeyGenerator
    {
        public ClientCredentialsCacheKey GenerateKey(ClientName clientName, TokenRequestParameters? parameters = null)
            => ClientCredentialsCacheKey.Parse(cacheKey);
    }
}
