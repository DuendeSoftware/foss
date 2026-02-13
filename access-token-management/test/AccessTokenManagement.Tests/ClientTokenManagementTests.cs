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

namespace Duende.AccessTokenManagement;

public class ClientTokenManagementTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly ServiceCollection _services = [];
    private readonly MockHttpMessageHandler _mockHttp = new();

    public ClientTokenManagementTests()
    {
        _services.AddSingleton<TimeProvider>(The.TimeProvider);
        _mockHttp.Fallback.Respond(req => throw new InvalidOperationException("no handler for " + req.RequestUri));
    }

    private TestData The { get; } = new();
    private TestDataBuilder Some => new(The);

    [Fact]
    public async Task Unknown_client_should_throw_exception()
    {
        _services.AddClientCredentialsTokenManagement();

        var provider = _services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var action = async () => await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("unknown"), ct: _ct);

        (await Should.ThrowAsync<OptionsValidationException>(action))
            .Message.ShouldContain("No ClientId configured for client unknown");
    }

    [Fact]
    public async Task Missing_client_id_throw_exception()
    {
        _services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                client.TokenEndpoint = new Uri("https://as/connect/token");
                client.ClientId = null;
                client.ClientSecret = ClientSecret.Parse("notnull");
            });

        var provider = _services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var action = async () => await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct);

        (await Should.ThrowAsync<OptionsValidationException>(action))
            .Message.ShouldContain("ClientId");
    }


    [Fact]
    public async Task Missing_client_secret_throw_exception()
    {

        var mockedRequest = _mockHttp.Expect("/connect/token")
            .Respond(_ => Some.TokenHttpResponse());

        _services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _mockHttp);

        _services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                client.TokenEndpoint = new Uri("https://as/connect/token");
                client.ClientId = The.ClientId;
                client.ClientSecret = null;
            });

        var provider = _services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();
        _mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());

    }

    [Fact]
    public async Task Missing_tokenEndpoint_throw_exception()
    {
        _services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                client.TokenEndpoint = null;
                client.ClientId = ClientId.Parse("test");
                client.ClientSecret = ClientSecret.Parse("notnull");
            });

        var provider = _services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var action = async () => await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct);

        (await Should.ThrowAsync<OptionsValidationException>(action))
            .Message.ShouldContain("TokenEndpoint");
    }

    [Theory]
    [InlineData(ClientCredentialStyle.AuthorizationHeader)]
    [InlineData(ClientCredentialStyle.PostBody)]
    public async Task Token_request_and_response_should_have_expected_values(ClientCredentialStyle style)
    {
        _services.AddClientCredentialsTokenManagement()
            .AddClient("test", client => Some.ClientCredentialsClient(client,
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
            _mockHttp.Expect("/connect/token")
                .WithFormData(expectedRequestFormData)
                .Respond(_ => Some.TokenHttpResponse());
        }
        else if (style == ClientCredentialStyle.AuthorizationHeader)
        {
            _mockHttp.Expect("/connect/token")
                .WithFormData(expectedRequestFormData)
                .WithHeaders("Authorization",
                    "Basic " + IdentityModel.Client.BasicAuthenticationOAuthHeaderValue.EncodeCredential(The.ClientId.ToString(), The.ClientSecret.ToString()))
                .Respond(_ => Some.TokenHttpResponse(Some.Token()));
        }

        _services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _mockHttp);

        var provider = _services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();
        _mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());
    }

    [Fact]
    public async Task Explicit_expires_in_response_should_create_token_with_expiration()
    {

        _services.AddClientCredentialsTokenManagement()
            .AddClient("test", client => Some.ClientCredentialsClient(client));

        _mockHttp.Expect(The.TokenEndpoint.ToString())
            .Respond(_ => Some.TokenHttpResponse(Some.Token() with
            {
                expires_in = 300
            }));

        _services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _mockHttp);

        var provider = _services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();
        _mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken() with
        {
            Expiration = The.CurrentDateTime.Add(TimeSpan.FromSeconds(300))
        });
    }

    [Fact]
    public async Task Missing_expires_in_response_should_create_long_lived_token()
    {
        _services.AddClientCredentialsTokenManagement()
            .AddClient("test", client => Some.ClientCredentialsClient(client));



        _mockHttp.Expect(The.TokenEndpoint.ToString())
            .Respond(_ => Some.TokenHttpResponse(Some.Token() with
            {
                expires_in = null
            }));

        _services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _mockHttp);

        var provider = _services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();
        _mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());
    }

    [Fact]
    public async Task Request_parameters_should_take_precedence_over_configuration()
    {
        _services.AddClientCredentialsTokenManagement()
            .AddClient("test", client => Some.ClientCredentialsClient(client,
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



        _mockHttp.Expect("/connect/token")
            .WithFormData(expectedRequestFormData)
            .Respond(_ => Some.TokenHttpResponse(Some.Token() with
            {
                scope = "scope_per_request"
            }));

        _services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _mockHttp);

        var provider = _services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), request, _ct).GetToken();
        _mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken() with
        {
            Scope = Scope.Parse("scope_per_request")
        });
    }

    [Fact]
    public async Task Request_assertions_should_be_sent_correctly()
    {
        _services.AddClientCredentialsTokenManagement()
            .AddClient("test", client => Some.ClientCredentialsClient(client, resource: The.Resource));

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



        _mockHttp.Expect("/connect/token")
            .WithFormData(expectedRequestFormData)
            .Respond(_ => Some.TokenHttpResponse());

        _services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _mockHttp);

        var provider = _services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), request, _ct).GetToken();
        _mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());
    }

    [Fact]
    public async Task Service_assertions_should_be_sent_correctly()
    {
        _services.AddClientCredentialsTokenManagement()
            .AddClient("test", client => Some.ClientCredentialsClient(client, resource: The.Resource));

        _services.AddTransient<IClientAssertionService>(_ =>
            new TestClientAssertionService("test", "service_type", "service_value"));

        var expectedRequestFormData = new Dictionary<string, string>
        {
            { OidcConstants.TokenRequest.ClientAssertionType, "service_type" },
            { OidcConstants.TokenRequest.ClientAssertion, "service_value" },
        };



        _mockHttp.Expect("/connect/token")
            .WithFormData(expectedRequestFormData)
            .Respond(_ => Some.TokenHttpResponse());

        _services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _mockHttp);

        var provider = _services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();
        _mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());
    }

    [Fact]
    public async Task Request_assertion_should_take_precedence_over_service_assertion()
    {
        _services.AddClientCredentialsTokenManagement()
            .AddClient("test", client => Some.ClientCredentialsClient(client, resource: The.Resource));

        _services.AddTransient<IClientAssertionService>(_ =>
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



        _mockHttp.Expect("/connect/token")
            .WithFormData(expectedRequestFormData)
            .Respond(_ => Some.TokenHttpResponse());

        _services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _mockHttp);

        var provider = _services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), request, _ct).GetToken();
        _mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());
    }

    [Fact]
    public async Task Service_should_hit_network_only_once_and_then_use_cache()
    {
        _services.AddClientCredentialsTokenManagement()
            .AddClient("test", client => Some.ClientCredentialsClient(client));



        var mockedRequest = _mockHttp.Expect("/connect/token")
            .Respond(_ => Some.TokenHttpResponse());

        _services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _mockHttp);

        var provider = _services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();
        _mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());

        // 2nd request
        token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken());
        _mockHttp.GetMatchCount(mockedRequest).ShouldBe(1);
    }

    [Fact]
    public async Task Service_should_hit_network_when_cache_throws_exception()
    {
        _services.AddClientCredentialsTokenManagement()
            .AddClient("test", client => Some.ClientCredentialsClient(client));



        // Get the cache to throw exceptions
        var fakeHybridCache = new FakeHybridCache();
        _services.AddSingleton<HybridCache>(fakeHybridCache);
        fakeHybridCache.OnGetOrCreate = () => throw new InvalidOperationException("Cache error");

        var mockedRequest = _mockHttp.Expect("/connect/token")
            .Respond(_ => Some.TokenHttpResponse());

        _services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _mockHttp);

        var provider = _services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();
        _mockHttp.GetMatchCount(mockedRequest).ShouldBe(1);
    }

    [Fact]
    public async Task Service_should_always_hit_network_with_force_renewal()
    {
        _services.AddClientCredentialsTokenManagement()
            .AddClient("test", client => Some.ClientCredentialsClient(client));



        _mockHttp.Expect("/connect/token")
            .Respond(_ => Some.TokenHttpResponse());
        _services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _mockHttp);

        var provider = _services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();
        _mockHttp.VerifyNoOutstandingExpectation();

        token.AccessToken.ShouldBe(The.AccessToken);
        token.AccessTokenType.ShouldNotBeNull().ShouldBe(The.TokenType);

        // 2nd request
        _mockHttp.Expect("/connect/token")
            .Respond(_ => Some.TokenHttpResponse());

        token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), new TokenRequestParameters { ForceTokenRenewal = true }, _ct).GetToken();

        token.AccessToken.ShouldBe(The.AccessToken);
        token.AccessTokenType.ShouldNotBeNull().ShouldBe(The.TokenType);

    }

    [Fact]
    public async Task client_with_dpop_key_should_send_proof_token()
    {
        var proof = new TestDPoPProofService { ProofToken = "proof_token" };
        _services.AddSingleton<IDPoPProofService>(proof);

        _services.AddClientCredentialsTokenManagement()
            .AddClient("test", client => Some.ClientCredentialsClient(
                toConfigure: client,
                jsonWebKey: The.JsonWebKey));



        _mockHttp.Expect("/connect/token")
            .With(m => m.Headers.Any(h => h.Key == "DPoP" && h.Value.FirstOrDefault() == "proof_token"))
            .Respond(_ => Some.TokenHttpResponse());

        _services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _mockHttp);

        var provider = _services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();
        _mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken() with
        {
            DPoPJsonWebKey = The.JsonWebKey
        });
    }

    [Fact]
    public async Task client_should_use_nonce_when_sending_dpop_proof()
    {
        var proof = new TestDPoPProofService { ProofToken = "proof_token", AppendNonce = true };
        _services.AddSingleton<IDPoPProofService>(proof);

        _services.AddClientCredentialsTokenManagement()
            .AddClient("test", client => Some.ClientCredentialsClient(
                toConfigure: client,
                jsonWebKey: The.JsonWebKey));

        _mockHttp.Expect(The.TokenEndpoint.ToString())
            .With(m => m.Headers.Any(h => h.Key == "DPoP" && h.Value.FirstOrDefault() == "proof_token"))
            .Respond(HttpStatusCode.BadRequest,
                [new KeyValuePair<string, string>("DPoP-Nonce", "some_nonce")],
                "application/json",
                JsonSerializer.Serialize(new { error = "use_dpop_nonce" }));

        _mockHttp.Expect(The.TokenEndpoint.ToString())
            .With(m => m.Headers.Any(h => h.Key == "DPoP" && h.Value.First() == ("proof_tokensome_nonce")))
            .Respond(_ => Some.TokenHttpResponse());

        _services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _mockHttp);

        var provider = _services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManager>();

        var token = await sut.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();
        _mockHttp.VerifyNoOutstandingExpectation();

        token.ShouldBeEquivalentTo(Some.ClientCredentialsToken() with
        {
            DPoPJsonWebKey = The.JsonWebKey
        });
    }

    [Fact]
    public async Task Cache_auto_tuning_should_persist_across_transient_manager_instances()
    {
        var tokenExpiry = (int)TimeSpan.FromDays(7).TotalSeconds;

        var fakeCache = new FakeHybridCache();
        _services.AddSingleton<HybridCache>(fakeCache);

        _services.AddClientCredentialsTokenManagement(options =>
        {
            options.UseCacheAutoTuning = true;
            options.DefaultCacheLifetime = TimeSpan.FromSeconds(30);
            options.LocalCacheExpiration = TimeSpan.FromMinutes(10);
            options.CacheLifetimeBuffer = 60;
        })
        .AddClient("test", client => Some.ClientCredentialsClient(client));

        _mockHttp.Expect("/connect/token")
            .Respond(_ => Some.TokenHttpResponse(Some.Token() with
            {
                expires_in = tokenExpiry
            }));

        _services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => _mockHttp);

        var services = _services.BuildServiceProvider();

        // First request with first manager instance
        var firstManager = services.GetRequiredService<IClientCredentialsTokenManager>();
        var firstToken = await firstManager.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();
        _mockHttp.VerifyNoOutstandingExpectation();

        firstToken.Expiration.ShouldBe(The.CurrentDateTime.Add(TimeSpan.FromSeconds(tokenExpiry)));

        // Get the cache expiration used for the first request
        var firstRequestExpiration = fakeCache.LastOptions?.Expiration;
        // The first request doesn't know the token lifetime yet, so it should use DefaultCacheLifetime (30 seconds)
        firstRequestExpiration.ShouldBe(TimeSpan.FromSeconds(30));

        _mockHttp.Expect("/connect/token")
            .Respond(_ => Some.TokenHttpResponse(Some.Token() with
            {
                expires_in = tokenExpiry
            }));

        var secondManager = services.GetRequiredService<IClientCredentialsTokenManager>();
        var secondToken = await secondManager.GetAccessTokenAsync(ClientCredentialsClientName.Parse("test"), ct: _ct).GetToken();
        _mockHttp.VerifyNoOutstandingExpectation();

        secondToken.Expiration.ShouldBe(The.CurrentDateTime.Add(TimeSpan.FromSeconds(tokenExpiry)));

        // Get the cache expiration used for the second request
        var secondRequestExpiration = fakeCache.LastOptions?.Expiration;

        // Expect the lifetime to be auto-tuned based on the first token's lifetime minus the buffer
        var expectedExpiration = TimeSpan.FromSeconds(tokenExpiry) - TimeSpan.FromSeconds(60);
        secondRequestExpiration.ShouldBe(expectedExpiration,
            "Second request should use the auto-tuned cache duration learned from the first request");
    }
}
