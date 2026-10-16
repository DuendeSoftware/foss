// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Text;
using Duende.AccessTokenManagement.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RichardSzalay.MockHttp;

namespace Duende.AccessTokenManagement;

public class ClientCredentialsDiscoveryTests
{
    private const string Authority = "https://as";
    private const string DiscoveryAddress = Authority + "/.well-known/openid-configuration";
    private const string JwksAddress = Authority + "/jwks";
    private const string TokenEndpoint = Authority + "/connect/token";
    private const string DiscoveryDocument =
        """{"issuer":"https://as","token_endpoint":"https://as/connect/token","jwks_uri":"https://as/jwks"}""";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly ServiceCollection _services = [];
    private readonly MockHttpMessageHandler _mockHttp = new();

    private TestData The { get; } = new();
    private TestDataBuilder Some => new(The);

    public ClientCredentialsDiscoveryTests()
    {
        _mockHttp.Fallback.Respond(request => throw new InvalidOperationException("no handler for " + request.RequestUri));
        _services.AddSingleton<TimeProvider>(The.TimeProvider);
        _services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _mockHttp);
        _services.AddClientCredentialsTokenManagement()
            .AddClient("test", ConfigureClient);
    }

    [Fact]
    public void Discovery_policy_should_have_independent_secure_defaults()
    {
        // Arrange

        var first = new ClientCredentialsClient();
        var second = new ClientCredentialsClient();

        // Act

        first.DiscoveryPolicy.RequireHttps = false;

        // Assert

        first.DiscoveryPolicy.ShouldNotBeSameAs(second.DiscoveryPolicy);
        second.DiscoveryPolicy.RequireHttps.ShouldBeTrue();
        second.DiscoveryPolicy.AllowHttpOnLoopback.ShouldBeTrue();
        second.DiscoveryPolicy.ValidateIssuerName.ShouldBeTrue();
        second.DiscoveryPolicy.ValidateEndpoints.ShouldBeTrue();
        second.DiscoveryPolicy.RequireKeySet.ShouldBeTrue();
    }

    [Fact]
    public void Creating_client_options_should_not_start_discovery()
    {
        // Arrange

        var discoveryRequest = MockDiscovery();
        using var provider = CreateProvider();

        // Act

        var options = provider.GetRequiredService<IOptionsMonitor<ClientCredentialsClient>>().Get("test");

        // Assert

        options.DiscoveredTokenEndpoint.ShouldNotBeNull();
        options.TokenEndpoint.ShouldBeNull();
        _mockHttp.GetMatchCount(discoveryRequest).ShouldBe(0);
    }

    [Theory]
    [InlineData("https://as/", "https://as", null, DiscoveryAddress)]
    [InlineData("https://as/tenant", "https://as/tenant", null, "https://as/tenant/.well-known/openid-configuration")]
    [InlineData("https://as/tenant/.well-known/openid-configuration", "https://as/tenant", null, "https://as/tenant/.well-known/openid-configuration")]
    [InlineData("https://as", "https://as", "custom/metadata", "https://as/custom/metadata")]
    public async Task Authority_should_resolve_the_configured_discovery_path(
        string authority, string issuer, string? discoveryPath, string expectedAddress)
    {
        // Arrange

        _mockHttp.Expect(expectedAddress)
            .Respond("application/json",
                $$"""{"issuer":"{{issuer}}","token_endpoint":"{{issuer}}/connect/token","jwks_uri":"{{issuer}}/jwks"}""");
        _mockHttp.Expect($"{issuer}/jwks").Respond("application/json", """{"keys":[]}""");
        _mockHttp.Expect($"{issuer}/connect/token").Respond(_ => Some.TokenHttpResponse());

        using var provider = CreateProvider(client =>
        {
            client.Authority = authority;
            client.DiscoveryPolicy.DiscoveryDocumentPath = discoveryPath;
        });
        var sut = provider.GetRequiredService<IClientCredentialsTokenEndpoint>();

        // Act

        var result = await sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct);

        // Assert

        result.Succeeded.ShouldBeTrue();
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Theory]
    [InlineData(null, "Issuer name is missing")]
    [InlineData("", "Issuer name is missing")]
    [InlineData("https://other", "Issuer name does not match authority")]
    public async Task Missing_or_mismatched_issuer_should_fail_discovery(string? issuer, string error)
    {
        // Arrange

        _mockHttp.Expect(DiscoveryAddress)
            .Respond("application/json",
                $$"""{"issuer":"{{issuer}}","token_endpoint":"https://as/connect/token","jwks_uri":"https://as/jwks"}""");
        using var provider = CreateProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenEndpoint>();

        // Act

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct));

        // Assert

        exception.Message.ShouldStartWith($"Policy error while contacting the discovery endpoint {Authority}:");
        exception.Message.ShouldContain(error);
        _mockHttp.GetMatchCount(_mockHttp.Fallback).ShouldBe(0);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Issuer_validation_should_be_configurable()
    {
        // Arrange

        _mockHttp.Expect(DiscoveryAddress)
            .Respond("application/json",
                """{"issuer":"https://other","token_endpoint":"https://as/connect/token","jwks_uri":"https://as/jwks"}""");
        MockKeys();
        MockTokenRequests();
        using var provider = CreateProvider(client => client.DiscoveryPolicy.ValidateIssuerName = false);
        var sut = provider.GetRequiredService<IClientCredentialsTokenEndpoint>();

        // Act

        var result = await sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct);

        // Assert

        result.Succeeded.ShouldBeTrue();
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Signing_key_requirement_should_follow_discovery_policy(bool requireKeySet)
    {
        // Arrange

        _mockHttp.Expect(DiscoveryAddress)
            .Respond("application/json", """{"issuer":"https://as","token_endpoint":"https://as/connect/token"}""");
        var tokenRequest = MockTokenRequests();
        using var provider = CreateProvider(client => client.DiscoveryPolicy.RequireKeySet = requireKeySet);
        var sut = provider.GetRequiredService<IClientCredentialsTokenEndpoint>();

        // Act

        if (requireKeySet)
        {
            var exception = await Should.ThrowAsync<InvalidOperationException>(
                () => sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct));

            // Assert

            exception.Message.ShouldBe($"Policy error while contacting the discovery endpoint {Authority}: Keyset is missing");
            _mockHttp.GetMatchCount(tokenRequest).ShouldBe(0);
        }
        else
        {
            var result = await sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct);

            // Assert

            result.Succeeded.ShouldBeTrue();
            _mockHttp.GetMatchCount(tokenRequest).ShouldBe(1);
        }

        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Http_discovery_should_require_explicit_policy_opt_out()
    {
        // Arrange

        const string authority = "http://authority.example";
        _mockHttp.Expect($"{authority}/.well-known/openid-configuration")
            .Respond("application/json",
                $$"""{"issuer":"{{authority}}","token_endpoint":"{{authority}}/token","jwks_uri":"{{authority}}/jwks"}""");
        _mockHttp.Expect($"{authority}/jwks").Respond("application/json", """{"keys":[]}""");
        _mockHttp.Expect($"{authority}/token").Respond(_ => Some.TokenHttpResponse());
        using var provider = CreateProvider(client =>
        {
            client.Authority = authority;
            client.DiscoveryPolicy.RequireHttps = false;
        });
        var sut = provider.GetRequiredService<IClientCredentialsTokenEndpoint>();

        // Act

        var result = await sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct);

        // Assert

        result.Succeeded.ShouldBeTrue();
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Discovery_should_use_the_configured_backchannel(bool useHttpClientInstance)
    {
        // Arrange

        using var customHandler = new MockHttpMessageHandler();
        using var customClient = customHandler.ToHttpClient();
        customHandler.Expect(DiscoveryAddress).Respond("application/json", DiscoveryDocument);
        customHandler.Expect(JwksAddress).Respond("application/json", """{"keys":[]}""");
        customHandler.Expect(TokenEndpoint).Respond(_ => Some.TokenHttpResponse());
        _services.AddHttpClient("custom").ConfigurePrimaryHttpMessageHandler(() => customHandler);
        using var provider = CreateProvider(client =>
        {
            client.HttpClientName = "custom";
            if (useHttpClientInstance)
            {
                client.HttpClientName = "not-used";
                client.HttpClient = customClient;
            }
        });
        var sut = provider.GetRequiredService<IClientCredentialsTokenEndpoint>();

        // Act

        var result = await sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct);

        // Assert

        result.Succeeded.ShouldBeTrue();
        customHandler.VerifyNoOutstandingExpectation();
        _mockHttp.GetMatchCount(_mockHttp.Fallback).ShouldBe(0);
    }

    [Fact]
    public async Task Concurrent_first_requests_should_share_discovery_without_mutating_options()
    {
        // Arrange

        var discoveryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var discoveryResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var discoveryRequest = _mockHttp.When(DiscoveryAddress).Respond(_ =>
        {
            discoveryStarted.TrySetResult();
            return discoveryResponse.Task;
        });
        var keysRequest = MockKeys();
        var tokenRequest = MockTokenRequests();
        using var provider = CreateProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<ClientCredentialsClient>>().Get("test");

        // Act

        var requests = Enumerable.Range(0, 16)
            .Select(_ => provider.GetRequiredService<IClientCredentialsTokenEndpoint>()
                .RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct))
            .ToArray();

        try
        {
            await discoveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), _ct);
        }
        finally
        {
            discoveryResponse.SetResult(CreateDiscoveryResponse());
        }

        var results = await Task.WhenAll(requests).WaitAsync(_ct);

        // Assert

        results.ShouldAllBe(result => result.Succeeded);
        _mockHttp.GetMatchCount(discoveryRequest).ShouldBe(1);
        _mockHttp.GetMatchCount(keysRequest).ShouldBe(1);
        _mockHttp.GetMatchCount(tokenRequest).ShouldBe(16);
        options.TokenEndpoint.ShouldBeNull();
    }

    [Fact]
    public async Task Canceling_one_request_should_not_cancel_shared_discovery()
    {
        // Arrange

        var discoveryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var discoveryResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var discoveryRequest = _mockHttp.When(DiscoveryAddress).Respond(_ =>
        {
            discoveryStarted.TrySetResult();
            return discoveryResponse.Task;
        });
        MockKeys();
        var tokenRequest = MockTokenRequests();
        using var provider = CreateProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_ct);
        var sut = provider.GetRequiredService<IClientCredentialsTokenEndpoint>();

        // Act

        var canceledRequest = sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: cancellation.Token);
        var otherRequest = sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct);

        try
        {
            await discoveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), _ct);
            await cancellation.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => canceledRequest);
        }
        finally
        {
            discoveryResponse.SetResult(CreateDiscoveryResponse());
        }

        var result = await otherRequest.WaitAsync(_ct);

        // Assert

        result.Succeeded.ShouldBeTrue();
        _mockHttp.GetMatchCount(discoveryRequest).ShouldBe(1);
        _mockHttp.GetMatchCount(tokenRequest).ShouldBe(1);
    }

    [Fact]
    public async Task Canceled_first_request_should_not_start_discovery()
    {
        // Arrange

        var discoveryRequest = MockDiscovery();
        MockKeys();
        MockTokenRequests();
        using var provider = CreateProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenEndpoint>();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act

        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: cancellation.Token));

        // Assert

        _mockHttp.GetMatchCount(discoveryRequest).ShouldBe(0);
        var result = await sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct);
        result.Succeeded.ShouldBeTrue();
        _mockHttp.GetMatchCount(discoveryRequest).ShouldBe(1);
    }

    [Fact]
    public async Task Each_named_client_should_have_its_own_discovery_result()
    {
        // Arrange

        var discoveryRequest = MockDiscovery();
        MockKeys();
        MockTokenRequests();
        _services.Configure<ClientCredentialsClient>("other", ConfigureClient);
        using var provider = CreateProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenEndpoint>();

        // Act

        var results = await Task.WhenAll(
            sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct),
            sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("other"), ct: _ct));

        // Assert

        results.ShouldAllBe(result => result.Succeeded);
        _mockHttp.GetMatchCount(discoveryRequest).ShouldBe(2);
    }

    [Fact]
    public async Task Recreating_options_should_resolve_discovery_again()
    {
        // Arrange

        var discoveryRequest = MockDiscovery();
        MockKeys();
        MockTokenRequests();
        using var provider = CreateProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<ClientCredentialsClient>>();
        var original = monitor.Get("test");
        var sut = provider.GetRequiredService<IClientCredentialsTokenEndpoint>();
        await sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct);

        // Act

        provider.GetRequiredService<IOptionsMonitorCache<ClientCredentialsClient>>().TryRemove("test").ShouldBeTrue();
        var result = await sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct);

        // Assert

        result.Succeeded.ShouldBeTrue();
        monitor.Get("test").ShouldNotBeSameAs(original);
        monitor.Get("test").TokenEndpoint.ShouldBeNull();
        original.TokenEndpoint.ShouldBeNull();
        _mockHttp.GetMatchCount(discoveryRequest).ShouldBe(2);
    }

    [Fact]
    public async Task Failed_discovery_should_be_retried_on_the_next_request()
    {
        // Arrange

        _mockHttp.Expect(DiscoveryAddress).Respond(HttpStatusCode.ServiceUnavailable);
        _mockHttp.Expect(DiscoveryAddress).Respond("application/json", DiscoveryDocument);
        _mockHttp.Expect(JwksAddress).Respond("application/json", """{"keys":[]}""");
        _mockHttp.Expect(TokenEndpoint).Respond(_ => Some.TokenHttpResponse());
        using var provider = CreateProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenEndpoint>();
        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct));

        // Act

        var result = await sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct);

        // Assert

        result.Succeeded.ShouldBeTrue();
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "Error retrieving discovery document from https://as:")]
    [InlineData(HttpStatusCode.Unauthorized, "Discovery endpoint https://as is unavailable:")]
    [InlineData(HttpStatusCode.NotFound, "Discovery endpoint https://as is unavailable:")]
    public async Task Discovery_http_failures_should_throw_consistent_invalid_operation_errors(
        HttpStatusCode statusCode, string messagePrefix)
    {
        // Arrange

        _mockHttp.Expect(DiscoveryAddress).Respond(statusCode);
        using var provider = CreateProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenEndpoint>();

        // Act

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct));

        // Assert

        exception.Message.ShouldStartWith(messagePrefix);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    private void ConfigureClient(ClientCredentialsClient client)
    {
        client.Authority = Authority;
        client.ClientId = The.ClientId;
        client.ClientSecret = The.ClientSecret;
    }

    private ServiceProvider CreateProvider(Action<ClientCredentialsClient>? configure = null)
    {
        if (configure is not null)
        {
            _services.Configure("test", configure);
        }

        return _services.BuildServiceProvider();
    }

    private MockedRequest MockDiscovery() =>
        _mockHttp.When(DiscoveryAddress).Respond("application/json", DiscoveryDocument);

    private MockedRequest MockKeys() =>
        _mockHttp.When(JwksAddress).Respond("application/json", """{"keys":[]}""");

    private MockedRequest MockTokenRequests() =>
        _mockHttp.When(TokenEndpoint).Respond(_ => Some.TokenHttpResponse());

    private static HttpResponseMessage CreateDiscoveryResponse() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(DiscoveryDocument, Encoding.UTF8, "application/json")
        };
}
