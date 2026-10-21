// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Text;
using System.Text.Json;
using Duende.AccessTokenManagement.Framework;
using Duende.AccessTokenManagement.Internal;
using Duende.IdentityModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
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

    [Fact]
    public async Task Authority_configured_in_later_post_configure_should_be_used()
    {
        // Arrange

        var discoveryRequest = MockDiscovery();
        MockKeys();
        MockTokenRequests();
        _services.PostConfigure<ClientCredentialsClient>("test", client => client.Authority = Authority);
        using var provider = CreateProvider(client => client.Authority = null);
        var sut = provider.GetRequiredService<IClientCredentialsTokenEndpoint>();

        // Act

        var result = await sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct);

        // Assert

        result.Succeeded.ShouldBeTrue();
        _mockHttp.GetMatchCount(discoveryRequest).ShouldBe(1);
        provider.GetRequiredService<IOptionsMonitor<ClientCredentialsClient>>().Get("test").TokenEndpoint.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Lazy_discovery_should_report_missing_authority_using_client_name(string? authority)
    {
        // Arrange

        using var provider = CreateProvider();
        var options = new ClientCredentialsClient { Authority = authority, ClientId = The.ClientId };
        var postConfigure = new PostConfigureClientCredentialsClient(provider.GetRequiredService<IHttpClientFactory>());
        postConfigure.PostConfigure("test", options);
        var discovery = options.DiscoveredTokenEndpoint.ShouldNotBeNull();

        // Act

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => discovery.Value.WaitAsync(_ct));

        // Assert

        exception.Message.ShouldBe("No TokenEndpoint or Authority configured for client test");
        _mockHttp.GetMatchCount(_mockHttp.Fallback).ShouldBe(0);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DPoP_proof_should_use_discovered_token_endpoint(bool retryWithNonce)
    {
        // Arrange

        var discoveryRequest = MockDiscovery();
        MockKeys();
        var proofs = new List<JsonWebToken>();
        var tokenRequest = _mockHttp.When(HttpMethod.Post, TokenEndpoint).Respond(request =>
        {
            var proof = new JsonWebTokenHandler().ReadJsonWebToken(
                request.Headers.GetValues(OidcConstants.HttpHeaders.DPoP).Single());
            proofs.Add(proof);

            if (retryWithNonce && proofs.Count == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Headers = { { OidcConstants.HttpHeaders.DPoPNonce, "some_nonce" } },
                    Content = new StringContent("""{"error":"use_dpop_nonce"}""", Encoding.UTF8, "application/json")
                };
            }

            return Some.TokenHttpResponse(Some.Token() with { token_type = "DPoP" });
        });
        using var provider = CreateProvider(client => client.DPoPJsonWebKey = The.JsonWebKey);
        var sut = provider.GetRequiredService<IClientCredentialsTokenEndpoint>();

        // Act

        var token = await sut.RequestAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();

        // Assert

        var expectedRequestCount = retryWithNonce ? 2 : 1;
        proofs.Count.ShouldBe(expectedRequestCount);
        proofs.ShouldAllBe(proof => proof.Claims.Single(claim => claim.Type == JwtClaimTypes.DPoPHttpUrl).Value == TokenEndpoint);
        proofs.ShouldAllBe(proof => proof.Claims.Single(claim => claim.Type == JwtClaimTypes.DPoPHttpMethod).Value == "POST");
        proofs[0].Claims.ShouldNotContain(claim => claim.Type == JwtClaimTypes.Nonce);
        if (retryWithNonce)
        {
            proofs[1].Claims.Single(claim => claim.Type == JwtClaimTypes.Nonce).Value.ShouldBe("some_nonce");
        }
        token.DPoPJsonWebKey.ShouldBe(The.JsonWebKey);
        _mockHttp.GetMatchCount(discoveryRequest).ShouldBe(1);
        _mockHttp.GetMatchCount(tokenRequest).ShouldBe(expectedRequestCount);
        provider.GetRequiredService<IOptionsMonitor<ClientCredentialsClient>>().Get("test").TokenEndpoint.ShouldBeNull();
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

    [Fact]
    public async Task Missing_tokenEndpoint_with_authority_should_discover_token_endpoint()
    {
        // Arrange

        MockDiscovery();
        MockKeys();
        MockTokenRequests();
        using var provider = CreateProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        // Act

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();

        // Assert

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());
    }

    [Fact]
    public async Task Metadata_without_token_endpoint_should_throw_exception()
    {
        // Arrange

        MockDiscovery("""{"issuer":"https://as","jwks_uri":"https://as/jwks"}""");
        MockKeys();
        using var provider = CreateProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        // Act

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct));

        // Assert

        exception.Message.ShouldBe("No token endpoint found in discovery document for client test");
    }

    [Fact]
    public async Task Metadata_request_failure_should_throw_exception()
    {
        // Arrange

        _mockHttp.Expect(DiscoveryAddress).Respond(HttpStatusCode.InternalServerError);
        using var provider = CreateProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        // Act

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct));

        // Assert

        exception.Message.ShouldStartWith($"Discovery endpoint {Authority} is unavailable:");
        exception.Message.ShouldContain("Internal Server Error");
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Discovered_tokenEndpoint_should_be_reused()
    {
        // Arrange

        var discoveryRequest = MockDiscovery();
        var keysRequest = MockKeys();
        var tokenRequest = MockTokenRequests();
        using var provider = CreateProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();
        await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();

        // Act

        await sut.GetAccessTokenAsync(
            ClientCredentialsClientName.Parse("test"),
            new TokenRequestParameters { ForceTokenRenewal = true },
            _ct).GetToken();

        // Assert

        _mockHttp.GetMatchCount(discoveryRequest).ShouldBe(1);
        _mockHttp.GetMatchCount(keysRequest).ShouldBe(1);
        _mockHttp.GetMatchCount(tokenRequest).ShouldBe(2);
        provider.GetRequiredService<IOptionsMonitor<ClientCredentialsClient>>().Get("test").TokenEndpoint.ShouldBeNull();
    }

    [Theory]
    [InlineData("https://as/.well-known/openid-configuration", null)]
    [InlineData("https://as/.well-known/oauth-authorization-server", ".well-known/oauth-authorization-server")]
    [InlineData("https://as/custom/metadata", "custom/metadata")]
    [InlineData("https://as/custom/metadata?tenant=test", "custom/metadata?tenant=test")]
    public async Task Full_discovery_document_url_should_be_supported(string address, string? discoveryPath)
    {
        // Arrange

        _mockHttp.Expect(address).Respond("application/json", DiscoveryDocument);
        MockKeys();
        MockTokenRequests();
        using var provider = CreateProvider(client =>
        {
            client.Authority = address;
            client.DiscoveryPolicy.DiscoveryDocumentPath = discoveryPath;
        });
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        // Act

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();

        // Assert

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Advertised_jwks_endpoint_failure_should_throw_exception()
    {
        // Arrange

        MockDiscovery();
        var jwksRequest = _mockHttp.When(JwksAddress).Respond(HttpStatusCode.ServiceUnavailable);
        using var provider = CreateProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        // Act

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct));

        // Assert

        exception.Message.ShouldStartWith($"Discovery endpoint {Authority} is unavailable:");
        exception.Message.ShouldContain(JwksAddress);
        _mockHttp.GetMatchCount(jwksRequest).ShouldBe(1);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Theory]
    [InlineData("authorization_endpoint", "https://signin.example/authorize")]
    [InlineData("userinfo_endpoint", "https://profiles.example/userinfo")]
    [InlineData("jwks_uri", "https://keys.example/jwks")]
    [InlineData("check_session_iframe", "https://sessions.example/check")]
    [InlineData("revocation_endpoint", "https://revocation.example/revoke")]
    [InlineData("introspection_endpoint", "https://introspection.example/introspect")]
    [InlineData("custom_extension_endpoint", "https://extensions.example/custom")]
    public async Task Discovery_policy_should_allow_additional_endpoint_hosts(string endpointName, string endpointAddress)
    {
        // Arrange

        MockDiscovery(
            $$"""{"issuer":"https://as","token_endpoint":"https://as/connect/token","{{endpointName}}":"{{endpointAddress}}"}""");
        if (endpointName == "jwks_uri")
        {
            MockKeys(endpointAddress);
        }
        MockTokenRequests();
        using var provider = CreateProvider(client =>
        {
            client.DiscoveryPolicy.RequireKeySet = false;
            client.DiscoveryPolicy.AdditionalEndpointBaseAddresses.Add(
                new Uri(endpointAddress).GetLeftPart(UriPartial.Authority));
        });
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        // Act

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();

        // Assert

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());
        _mockHttp.GetMatchCount(_mockHttp.Fallback).ShouldBe(0);
    }

    [Theory]
    [InlineData("http://as/.well-known/openid-configuration")]
    [InlineData("http://192.0.2.1/custom/metadata")]
    public async Task Non_loopback_http_metadata_should_be_rejected(string address)
    {
        // Arrange

        var metadataRequest = _mockHttp.When(address)
            .Respond("application/json", """{"token_endpoint":"http://as/connect/token"}""");
        using var provider = CreateProvider(client => client.Authority = address);
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        // Act

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct));

        // Assert

        exception.Message.ShouldStartWith($"Error parsing discovery document from {address}:");
        exception.Message.ShouldContain("HTTPS required");
        _mockHttp.GetMatchCount(metadataRequest).ShouldBe(0);
        _mockHttp.GetMatchCount(_mockHttp.Fallback).ShouldBe(0);
    }

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://localhost:5000")]
    public async Task Loopback_http_metadata_and_token_endpoint_should_be_allowed(string authority)
    {
        // Arrange

        var metadataAddress = $"{authority}/.well-known/openid-configuration";
        var tokenEndpoint = $"{authority}/connect/token";
        _mockHttp.Expect(metadataAddress)
            .Respond("application/json",
                $$"""{"issuer":"{{authority}}","token_endpoint":"{{tokenEndpoint}}","jwks_uri":"{{authority}}/jwks"}""");
        MockKeys($"{authority}/jwks");
        MockTokenRequests(tokenEndpoint);
        using var provider = CreateProvider(client => client.Authority = metadataAddress);
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        // Act

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();

        // Assert

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Theory]
    [InlineData("http://as/connect/token", "Endpoint does not use HTTPS")]
    [InlineData("https://other/connect/token", "Endpoint is on a different host than authority")]
    [InlineData("https://as.other.example/connect/token", "Endpoint is on a different host than authority")]
    [InlineData("https://as:8443/connect/token", "Endpoint is on a different host than authority")]
    [InlineData("ftp://as/connect/token", "Malformed endpoint")]
    [InlineData("not-a-uri", "Malformed endpoint")]
    public async Task Invalid_discovered_token_endpoint_should_preserve_policy_error(string tokenEndpoint, string error)
    {
        // Arrange

        MockDiscovery($$"""{"issuer":"https://as","token_endpoint":"{{tokenEndpoint}}"}""");
        using var provider = CreateProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        // Act

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct));

        // Assert

        exception.Message.ShouldContain(error);
        exception.Message.ShouldContain(tokenEndpoint);
    }

    [Fact]
    public async Task Malformed_metadata_json_should_preserve_underlying_exception()
    {
        // Arrange

        MockDiscovery("""{"token_endpoint":""");
        using var provider = CreateProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        // Act

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct));

        // Assert

        exception.Message.ShouldContain(Authority);
        exception.Message.ShouldNotContain("No token endpoint found");
        exception.InnerException.ShouldBeAssignableTo<JsonException>();
    }

    [Fact]
    public async Task Metadata_network_failure_should_preserve_original_exception()
    {
        // Arrange

        var failure = new HttpRequestException("TLS handshake failed");
        _mockHttp.Expect(DiscoveryAddress).Respond(_ => throw failure);
        using var provider = CreateProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        // Act

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct));

        // Assert

        exception.Message.ShouldStartWith($"Error parsing discovery document from {Authority}:");
        exception.InnerException.ShouldBeSameAs(failure);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"https://as/connect/token\"")]
    public async Task Invalid_metadata_document_shape_should_throw_exception(string document)
    {
        // Arrange

        MockDiscovery(document);
        using var provider = CreateProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        // Act

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct));

        // Assert

        exception.Message.ShouldStartWith($"Error parsing discovery document from {Authority}:");
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("ftp://as/metadata")]
    public async Task Invalid_authority_should_throw_exception(string address)
    {
        // Arrange

        using var provider = CreateProvider(client => client.Authority = address);
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        // Act

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct));

        // Assert

        exception.Message.ShouldBe("Malformed URL");
        _mockHttp.GetMatchCount(_mockHttp.Fallback).ShouldBe(0);
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

    private MockedRequest MockDiscovery(string document = DiscoveryDocument) =>
        _mockHttp.When(DiscoveryAddress).Respond("application/json", document);

    private MockedRequest MockKeys(string address = JwksAddress) =>
        _mockHttp.When(address).Respond("application/json", """{"keys":[]}""");

    private MockedRequest MockTokenRequests(string address = TokenEndpoint) =>
        _mockHttp.When(address).Respond(_ => Some.TokenHttpResponse());

    private static HttpResponseMessage CreateDiscoveryResponse() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(DiscoveryDocument, Encoding.UTF8, "application/json")
        };
}
