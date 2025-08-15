// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Buffers;
using System.Net;
using System.Text;
using System.Text.Json;
using Duende.AccessTokenManagement.Framework;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;

namespace Duende.AccessTokenManagement;

public class BackChannelClientTests(ITestOutputHelper output)
{
    public TestData The { get; } = new();
    public TestDataBuilder Some => new(The);


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
        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"));
        token.Succeeded.ShouldBeTrue();
        token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"));
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
        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"));
        token.Succeeded.ShouldBeTrue();

        // then delete the token
        await sut.DeleteAccessTokenAsync(ClientCredentialsClientName.Parse("test"));

        // Now get another token. This should result in another call
        token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"));
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
                client.ClientId = ClientId.Parse("id");
                client.ClientSecret = ClientSecret.Parse("required");
            });

        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://as/*")
            .Respond((_) => new HttpResponseMessage(HttpStatusCode.NotFound));

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"));

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
                client.ClientId = ClientId.Parse("id");
                client.ClientSecret = ClientSecret.Parse("required");
                client.HttpClientName = "custom";
            });

        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://as/*")
            .Respond(HttpStatusCode.NotFound);

        services.AddHttpClient("custom")
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"));

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
                client.ClientId = ClientId.Parse("id");
                client.ClientSecret = ClientSecret.Parse("required");
                client.HttpClientName = "custom";
            });

        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://as/*")
            .Respond((_) => Some.TokenHttpResponse());

        services.AddHttpClient("custom")
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), new TokenRequestParameters
        {
            ForceTokenRenewal = new ForceTokenRenewal(false),
            Scope = Scope.Parse("scope1"),

        }).GetToken();


        await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), new TokenRequestParameters
        {
            ForceTokenRenewal = new ForceTokenRenewal(false),
            Scope = Scope.Parse("scope2"),

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
                client.ClientId = ClientId.Parse("id");
                client.ClientSecret = ClientSecret.Parse("required");
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

        var t1 = Task.Run(async () =>
        {
            await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), new TokenRequestParameters
            {
                ForceTokenRenewal = new ForceTokenRenewal(false),
                Scope = Scope.Parse("scope1"),

            }).GetToken();
        });
        await Task.Delay(100);


        var t2 = Task.Run(async () =>
        {
            await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), new TokenRequestParameters
            {
                ForceTokenRenewal = new ForceTokenRenewal(false),
                Scope = Scope.Parse("scope2"),

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
                client.ClientId = ClientId.Parse("id");
                client.ClientSecret = ClientSecret.Parse("required");
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

        var t1 = Task.Run(async () =>
        {
            await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), new TokenRequestParameters
            {
                ForceTokenRenewal = new ForceTokenRenewal(false),
                Parameters = new Parameters
                {
                    { "tenant", "1" }
                }

            }).GetToken();
        });
        await Task.Delay(100);


        var t2 = Task.Run(async () =>
        {
            await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), new TokenRequestParameters
            {
                ForceTokenRenewal = new ForceTokenRenewal(false),
                Parameters = new Parameters
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
        await t2.ThrowOnTimeout();

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
                client.ClientId = ClientId.Parse("id");
                client.ClientSecret = ClientSecret.Parse("required");
                client.HttpClient = mockClient;
            });

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"));

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
                client.ClientId = ClientId.Parse("id");
                client.ClientSecret = ClientSecret.Parse("required");
                client.HttpClientName = "custom";
            });

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://as/*")
            .Respond((_) => Some.TokenHttpResponse());

        services.AddHttpClient("custom")
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test")).GetToken();

        // Verify we actually used the cache
        replacementCache.GetOrCreateCount.ShouldBe(1);
    }


    [Fact]
    public async Task Can_use_custom_encryption()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        services.AddHybridCache()
            .AddSerializer<ClientCredentialsToken, EncryptedHybridCacheSerializer>();

        services.AddClientCredentialsTokenManagement()
            .AddClient(ClientCredentialsClientName.Parse("test"), client =>
            {
                client.TokenEndpoint = new Uri("https://as");
                client.ClientId = ClientId.Parse("id");
                client.ClientSecret = ClientSecret.Parse("required");
                client.HttpClientName = "custom";
            });

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://as/*")
            .Respond((_) => Some.TokenHttpResponse());

        services.AddHttpClient("custom")
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test")).GetToken();
        var token2 = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test")).GetToken();

        token.AccessToken.ShouldBeEquivalentTo(token2.AccessToken);
        var encryptedSerializer =
            ((EncryptedHybridCacheSerializer)
                provider.GetRequiredService<IHybridCacheSerializer<ClientCredentialsToken>>());

        encryptedSerializer.SerializedToken.ShouldBeEquivalentTo(token);
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
                client.ClientId = ClientId.Parse("id");
                client.ClientSecret = ClientSecret.Parse("required");
                client.HttpClientName = "custom";
            });

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://as/*")
            .Respond((_) => Some.TokenHttpResponse());

        services.AddHttpClient("custom")
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test")).GetToken();

        replacementCache.CacheKey.ShouldBe("always_the_same");

    }
    // This test proves that we can also support custom encryption of the token. 
    [Fact]
    public async Task Can_use_custom_serializer()
    {
        var services = new ServiceCollection();

        services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                Some.ClientCredentialsClient(client);
            });

        services.AddHybridCache().AddSerializer<ClientCredentialsToken, TestSerializer>();

        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When(The.TokenEndpoint.ToString())
            .Respond((_) => Some.TokenHttpResponse());

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        // Getting the token twice should result in a single call (because it' cached)
        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"));

        token.Token!.AccessToken.ToString().ShouldStartWith("_prefix_added_by_serializer");
        token.Token!.AccessToken.ToString().ShouldEndWith("_suffix_added_by_serializer_");

    }

    private class TestSerializer : IHybridCacheSerializer<ClientCredentialsToken>
    {
        public ClientCredentialsToken Deserialize(ReadOnlySequence<byte> source)
        {
            var text = Encoding.UTF8.GetString(source);
            return new ClientCredentialsToken
            {
                AccessToken = AccessToken.Parse(text + "_suffix_added_by_serializer_"),
                AccessTokenType = null,
                DPoPJsonWebKey = null,
                Expiration = DateTimeOffset.Now,
                Scope = null,
                ClientId = default
            };
        }

        public void Serialize(ClientCredentialsToken value, IBufferWriter<byte> target)
        {
            var bytes = Encoding.UTF8.GetBytes("_prefix_added_by_serializer_" + value.AccessToken.ToString());
            target.Write(bytes);
        }
    }

    [Theory]
    [InlineData(HybridCacheConstants.CacheTag)]
    [InlineData("some_client_name")]
    public async Task Can_delete_entries_for_entire_atm(string clientName)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IClientCredentialsCacheKeyGenerator>(new AlwaysSameKeyCacheKeyGenerator("always_the_same"));

        services.AddClientCredentialsTokenManagement()
            .AddClient("some_client_name", client =>
            {
                client.TokenEndpoint = new Uri("https://as");
                client.ClientId = ClientId.Parse("id");
                client.ClientSecret = ClientSecret.Parse("required");
                client.HttpClientName = "custom";
            });

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://as/*")
            .Respond((_) => Some.TokenHttpResponse());

        services.AddHttpClient("custom")
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("some_client_name")).GetToken();

        var cache = provider.GetRequiredService<HybridCache>();
        var cachedToken = await cache.GetOrCreateAsync<ClientCredentialsToken>("always_the_same",
            (_) => ValueTask.FromResult((ClientCredentialsToken)null!));

        cachedToken.ShouldNotBeNull();

        await cache.RemoveByTagAsync(clientName);

        cachedToken = await cache.GetOrCreateAsync<ClientCredentialsToken>("always_the_same",
            (_) => ValueTask.FromResult((ClientCredentialsToken)null!));
        cachedToken.ShouldBeNull();
    }

    /// <summary>
    /// Example on how to implement a serializer that encrypts data using ASP.NET Core Data Protection.
    /// </summary>
    public class EncryptedHybridCacheSerializer : IHybridCacheSerializer<ClientCredentialsToken>
    {
        private readonly IDataProtector _protector;

        public EncryptedHybridCacheSerializer(IDataProtectionProvider provider) => _protector = provider.CreateProtector("ClientCredentialsToken");

        public ClientCredentialsToken? SerializedToken;

        public ClientCredentialsToken Deserialize(ReadOnlySequence<byte> source)
        {
            // Convert the sequence to a byte array
            var buffer = source.ToArray();
            // Unprotect (decrypt) the data
            var unprotected = _protector.Unprotect(buffer);
            // Deserialize the JSON payload
            var deserialized = JsonSerializer.Deserialize<ClientCredentialsToken>(unprotected)!;

            SerializedToken.ShouldBeEquivalentTo(deserialized);
            return deserialized;
        }

        public void Serialize(ClientCredentialsToken value, IBufferWriter<byte> target)
        {
            SerializedToken = value;
            // Serialize the value to JSON
            var json = JsonSerializer.SerializeToUtf8Bytes(value);
            // Protect (encrypt) the data
            var protectedBytes = _protector.Protect(json);
            // Write to the buffer
            target.Write(protectedBytes);
        }
    }

    public class AlwaysSameKeyCacheKeyGenerator(string cacheKey) : IClientCredentialsCacheKeyGenerator
    {
        public ClientCredentialsCacheKey GenerateKey(ClientCredentialsClientName clientName, TokenRequestParameters? parameters = null)
            => ClientCredentialsCacheKey.Parse(cacheKey);
    }
}
