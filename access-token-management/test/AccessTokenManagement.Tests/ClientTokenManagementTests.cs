// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.Framework;

using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RichardSzalay.MockHttp;

namespace Duende.AccessTokenManagement.Tests;

public class ClientTokenManagementTests
{
    private ServiceCollection services = new ServiceCollection();
    private MockHttpMessageHandler mockHttp = new MockHttpMessageHandler();

    public ClientTokenManagementTests()
    {
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(() => The.CurrentDate));
        mockHttp.Fallback.Respond((req) => throw new InvalidOperationException("no handler for " + req.RequestUri));
    }

    public TestData The { get; } = new TestData();
    public TestDataBuilder Some => new TestDataBuilder(The);

    [Fact]
    public async Task Unknown_client_should_throw_exception()
    {
        services.AddClientCredentialsTokenManagement();

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var action = async () => await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("unknown"));

        (await Should.ThrowAsync<OptionsValidationException>(action))
            .Message.ShouldContain("No ClientId configured for client unknown");
    }

    [Fact]
    public async Task Missing_client_id_throw_exception()
    {
        services.AddClientCredentialsTokenManagement()
            .AddClient(ClientCredentialsClientName.Parse("test"), client =>
            {
                client.TokenEndpoint = new Uri("https://as/connect/token");
                client.ClientId = null;
                client.ClientSecret = ClientSecret.Parse("notnull");
            });

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var action = async () => await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"));

        (await Should.ThrowAsync<OptionsValidationException>(action))
            .Message.ShouldContain("ClientId");
    }


    [Fact]
    public async Task Missing_client_secret_throw_exception()
    {
        services.AddClientCredentialsTokenManagement()
            .AddClient(ClientCredentialsClientName.Parse("test"), client =>
            {
                client.TokenEndpoint = new Uri("https://as/connect/token");
                client.ClientId = ClientId.Parse("test");
                client.ClientSecret = null;
            });

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var action = async () => await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"));

        (await Should.ThrowAsync<OptionsValidationException>(action))
            .Message.ShouldContain("ClientId");
    }

    [Fact]
    public async Task Missing_tokenEndpoint_throw_exception()
    {
        services.AddClientCredentialsTokenManagement()
            .AddClient(ClientCredentialsClientName.Parse("test"), client =>
            {
                client.TokenEndpoint = null;
                client.ClientId = ClientId.Parse("test");
                client.ClientSecret = ClientSecret.Parse("notnull");
            });

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var action = async () => await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"));

        (await Should.ThrowAsync<OptionsValidationException>(action))
            .Message.ShouldContain("TokenEndpoint");
    }

    [Theory]
    [InlineData(ClientCredentialStyle.AuthorizationHeader)]
    [InlineData(ClientCredentialStyle.PostBody)]
    public async Task Token_request_and_response_should_have_expected_values(ClientCredentialStyle style)
    {
        services.AddClientCredentialsTokenManagement()
            .AddClient(ClientCredentialsClientName.Parse("test"), client => Some.ClientCredentialsClient(client,
                resource: The.Resource,
                style: style,
                parameters: new()
                {
                    ["audience"] = "audience"
                }));

        var expectedRequestFormData = new Dictionary<string, string>
        {
            { "scope", The.Scope.ToString() },
            { "resource", The.Resource.ToString() },
            { "audience", "audience" },
        };

        if (style == ClientCredentialStyle.PostBody)
        {
            expectedRequestFormData.Add("client_id", The.ClientId.ToString());
            expectedRequestFormData.Add("client_secret", The.ClientSecret.ToString());
        }

        if (style == ClientCredentialStyle.PostBody)
        {
            mockHttp.Expect("/connect/token")
                .WithFormData(expectedRequestFormData)
                .Respond(_ => Some.TokenHttpResponse());
        }
        else if (style == ClientCredentialStyle.AuthorizationHeader)
        {
            mockHttp.Expect("/connect/token")
                .WithFormData(expectedRequestFormData)
                .WithHeaders("Authorization",
                    "Basic " + IdentityModel.Client.BasicAuthenticationOAuthHeaderValue.EncodeCredential(The.ClientId.ToString(), The.ClientSecret.ToString()))
                .Respond(_ => Some.TokenHttpResponse(Some.Token()));
        }

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test")).GetToken();
        mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());
    }

    [Fact]
    public async Task Explicit_expires_in_response_should_create_token_with_expiration()
    {

        services.AddClientCredentialsTokenManagement()
            .AddClient(ClientCredentialsClientName.Parse("test"), client => Some.ClientCredentialsClient(client));

        mockHttp.Expect(The.TokenEndpoint.ToString())
            .Respond(_ => Some.TokenHttpResponse(Some.Token() with
            {
                expires_in = 300
            }));

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test")).GetToken();
        mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken() with
        {
            Expiration = The.CurrentDate.Add(TimeSpan.FromSeconds(300))
        });
    }
    [Fact]
    public async Task Missing_expires_in_response_should_create_long_lived_token()
    {
        services.AddClientCredentialsTokenManagement()
            .AddClient(ClientCredentialsClientName.Parse("test"), client => Some.ClientCredentialsClient(client));



        mockHttp.Expect(The.TokenEndpoint.ToString())
            .Respond(_ => Some.TokenHttpResponse(Some.Token() with
            {
                expires_in = null
            }));

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test")).GetToken();
        mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());
    }

    [Fact]
    public async Task Request_parameters_should_take_precedence_over_configuration()
    {
        services.AddClientCredentialsTokenManagement()
            .AddClient(ClientCredentialsClientName.Parse("test"), client => Some.ClientCredentialsClient(client,
                resource: The.Resource,
                parameters: new()
                {
                    ["audience"] = "audience"
                }));
        var request = new TokenRequestParameters
        {
            Scope = Scope.Parse("scope_per_request"),
            Resource = Resource.Parse("resource_per_request"),
            Parameters =
            {
                { "audience", "audience_per_request" },
            },
        };

        var expectedRequestFormData = new Dictionary<string, string>
        {
            { "scope", "scope_per_request" },
            { "resource", "resource_per_request" },
            { "audience", "audience_per_request" },
        };



        mockHttp.Expect("/connect/token")
            .WithFormData(expectedRequestFormData)
            .Respond(_ => Some.TokenHttpResponse(Some.Token() with
            {
                scope = "scope_per_request"
            }));

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), request).GetToken();
        mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken() with
        {
            Scope = Scope.Parse("scope_per_request")
        });
    }

    [Fact]
    public async Task Request_assertions_should_be_sent_correctly()
    {
        services.AddClientCredentialsTokenManagement()
            .AddClient(ClientCredentialsClientName.Parse("test"), client => Some.ClientCredentialsClient(client, resource: The.Resource));

        var request = new TokenRequestParameters
        {
            Assertion = new()
            {
                Type = "type",
                Value = "value"
            }
        };

        var expectedRequestFormData = new Dictionary<string, string>
        {
            { OidcConstants.TokenRequest.ClientAssertionType, "type" },
            { OidcConstants.TokenRequest.ClientAssertion, "value" },
        };



        mockHttp.Expect("/connect/token")
            .WithFormData(expectedRequestFormData)
            .Respond(_ => Some.TokenHttpResponse());

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), request).GetToken();
        mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());
    }

    [Fact]
    public async Task Service_assertions_should_be_sent_correctly()
    {
        services.AddClientCredentialsTokenManagement()
            .AddClient(ClientCredentialsClientName.Parse("test"), client => Some.ClientCredentialsClient(client, resource: The.Resource));

        services.AddTransient<IClientAssertionService>(_ =>
            new TestClientAssertionService("test", "service_type", "service_value"));

        var expectedRequestFormData = new Dictionary<string, string>
        {
            { OidcConstants.TokenRequest.ClientAssertionType, "service_type" },
            { OidcConstants.TokenRequest.ClientAssertion, "service_value" },
        };



        mockHttp.Expect("/connect/token")
            .WithFormData(expectedRequestFormData)
            .Respond(_ => Some.TokenHttpResponse());

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test")).GetToken();
        mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());
    }

    [Fact]
    public async Task Request_assertion_should_take_precedence_over_service_assertion()
    {
        services.AddClientCredentialsTokenManagement()
            .AddClient(ClientCredentialsClientName.Parse("test"), client => Some.ClientCredentialsClient(client, resource: The.Resource));

        services.AddTransient<IClientAssertionService>(_ =>
            new TestClientAssertionService("test", "service_type", "service_value"));

        var request = new TokenRequestParameters
        {
            Assertion = new()
            {
                Type = "request_type",
                Value = "request_value"
            }
        };

        var expectedRequestFormData = new Dictionary<string, string>
        {
            { OidcConstants.TokenRequest.ClientAssertionType, "request_type" },
            { OidcConstants.TokenRequest.ClientAssertion, "request_value" },
        };



        mockHttp.Expect("/connect/token")
            .WithFormData(expectedRequestFormData)
            .Respond(_ => Some.TokenHttpResponse());

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), request).GetToken();
        mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());
    }

    [Fact]
    public async Task Service_should_hit_network_only_once_and_then_use_cache()
    {
        services.AddClientCredentialsTokenManagement()
            .AddClient(ClientCredentialsClientName.Parse("test"), client => Some.ClientCredentialsClient(client));



        var mockedRequest = mockHttp.Expect("/connect/token")
            .Respond(_ => Some.TokenHttpResponse());

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test")).GetToken();
        mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());

        // 2nd request
        token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test")).GetToken();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());
        mockHttp.GetMatchCount(mockedRequest).ShouldBe(1);
    }

    [Fact]
    public async Task Service_should_hit_network_when_cache_throws_exception()
    {
        services.AddClientCredentialsTokenManagement()
            .AddClient(ClientCredentialsClientName.Parse("test"), client => Some.ClientCredentialsClient(client));



        // Get the cache to throw exceptions
        var fakeHybridCache = new FakeHybridCache();
        services.AddSingleton<HybridCache>(fakeHybridCache);
        fakeHybridCache.OnGetOrCreate = () => throw new InvalidOperationException("Cache error");

        var mockedRequest = mockHttp.Expect("/connect/token")
            .Respond(_ => Some.TokenHttpResponse());

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test")).GetToken();
        mockHttp.GetMatchCount(mockedRequest).ShouldBe(1);
    }

    [Fact]
    public async Task Service_should_always_hit_network_with_force_renewal()
    {
        services.AddClientCredentialsTokenManagement()
            .AddClient(ClientCredentialsClientName.Parse("test"), client => Some.ClientCredentialsClient(client));



        mockHttp.Expect("/connect/token")
            .Respond(_ => Some.TokenHttpResponse());
        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test")).GetToken();
        mockHttp.VerifyNoOutstandingExpectation();

        token.AccessToken.ShouldBe(The.AccessToken);
        token.AccessTokenType.ShouldNotBeNull().ShouldBe(The.TokenType);

        // 2nd request
        mockHttp.Expect("/connect/token")
            .Respond(_ => Some.TokenHttpResponse());

        token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), new TokenRequestParameters { ForceTokenRenewal = new ForceTokenRenewal(true) }).GetToken();

        token.AccessToken.ShouldBe(The.AccessToken);
        token.AccessTokenType.ShouldNotBeNull().ShouldBe(The.TokenType);

    }

    [Fact]
    public async Task client_with_dpop_key_should_send_proof_token()
    {
        var proof = new TestDPoPProofService() { ProofToken = "proof_token" };
        services.AddSingleton<IDPoPProofService>(proof);

        services.AddClientCredentialsTokenManagement()
            .AddClient(ClientCredentialsClientName.Parse("test"), client => Some.ClientCredentialsClient(
                toConfigure: client,
                jsonWebKey: The.JsonWebKey));



        mockHttp.Expect("/connect/token")
            .With(m => m.Headers.Any(h => h.Key == "DPoP" && h.Value.FirstOrDefault() == "proof_token"))
            .Respond(_ => Some.TokenHttpResponse());

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test")).GetToken();
        mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken() with
        {
            DPoPJsonWebKey = The.JsonWebKey
        });
    }

    [Fact]
    public async Task client_should_use_nonce_when_sending_dpop_proof()
    {
        var proof = new TestDPoPProofService() { ProofToken = "proof_token", AppendNonce = true };
        services.AddSingleton<IDPoPProofService>(proof);

        services.AddClientCredentialsTokenManagement()
            .AddClient(ClientCredentialsClientName.Parse("test"), client => Some.ClientCredentialsClient(
                toConfigure: client,
                jsonWebKey: The.JsonWebKey));

        mockHttp.Expect(The.TokenEndpoint.ToString())
            .With(m => m.Headers.Any(h => h.Key == "DPoP" && h.Value.FirstOrDefault() == "proof_token"))
            .Respond(HttpStatusCode.BadRequest,
                new[] { new KeyValuePair<string, string>("DPoP-Nonce", "some_nonce") },
                "application/json",
                JsonSerializer.Serialize(new { error = "use_dpop_nonce" }));

        mockHttp.Expect(The.TokenEndpoint.ToString())
            .With(m => m.Headers.Any(h => h.Key == "DPoP" && h.Value.First() == ("proof_tokensome_nonce")))
            .Respond(_ => Some.TokenHttpResponse());

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test")).GetToken();
        mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken() with
        {
            DPoPJsonWebKey = The.JsonWebKey
        });
    }
}
