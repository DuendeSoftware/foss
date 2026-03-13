// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient.DPoP.Framework;
using Duende.IdentityServer.Models;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityModel.OidcClient.DPoP;

public class DPoPTest : IntegrationTestBase
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static readonly string _jwkJson;
    private readonly IDPoPProofTokenFactory _proofTokenFactory;
    private readonly IdentityServer.Models.Client _client;

    static DPoPTest()
    {
        var key = IdentityServer.Configuration.CryptoHelper.CreateRsaSecurityKey();
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        jwk.Alg = "RS256";
        _jwkJson = JsonSerializer.Serialize(jwk);
    }

    public DPoPTest()
    {
        IdentityServerHost.ApiScopes.Add(new ApiScope("scope1"));

        IdentityServerHost.Clients.Add(_client = new IdentityServer.Models.Client
        {
            ClientId = "client_credentials_client",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = { "scope1" },
            RequireDPoP = true,
        });

        _proofTokenFactory = new DefaultDPoPProofTokenFactory(_jwkJson);
    }

    [Fact]
    public async Task dpop_tokens_should_be_passed_to_token_endpoint()
    {
        var handler = new ProofTokenMessageHandler(_proofTokenFactory, IdentityServerHost.Server.CreateHandler());
        var client = new HttpClient(handler);

        var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = IdentityServerHost.Url("/connect/token"),
            ClientId = "client_credentials_client",
            ClientSecret = "secret",
        }, _ct);

        tokenResponse.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        tokenResponse.TokenType.ShouldBe("DPoP");
    }

    [Fact]
    public async Task when_nonce_required_nonce_should_be_used_for_token_endpoint()
    {
        _client.DPoPValidationMode = DPoPTokenExpirationValidationMode.Nonce;

        var handler = new ProofTokenMessageHandler(_proofTokenFactory, IdentityServerHost.Server.CreateHandler());
        var client = new HttpClient(handler);

        var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = IdentityServerHost.Url("/connect/token"),
            ClientId = "client_credentials_client",
            ClientSecret = "secret",
        }, _ct);

        tokenResponse.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        tokenResponse.TokenType.ShouldBe("DPoP");
    }

    [Fact]
    public async Task dpop_tokens_should_be_passed_to_api()
    {
        var tokenHandler = new ProofTokenMessageHandler(_proofTokenFactory, IdentityServerHost.Server.CreateHandler());
        var tokenClient = new HttpClient(tokenHandler);

        var tokenResponse = await tokenClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = IdentityServerHost.Url("/connect/token"),
            ClientId = "client_credentials_client",
            ClientSecret = "secret",
        }, _ct);

        var apiHandler = new ProofTokenMessageHandler(_proofTokenFactory, ApiHost.Server.CreateHandler());
        var apiClient = new HttpClient(apiHandler);
        apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("DPoP", tokenResponse.AccessToken);

        ApiHost.ApiInvoked += ctx =>
        {
            ctx.User.Identity?.IsAuthenticated.ShouldBeTrue();
        };

        var apiResponse = await apiClient.GetAsync(ApiHost.Url("/api"), _ct);
        apiResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task when_nonce_required_nonce_should_be_used_for_api_endpoint()
    {
        ApiHost = new ApiHost(IdentityServerHost);
        ApiHost.ValidateNonce = true;
        await ApiHost.InitializeAsync();

        var tokenHandler = new ProofTokenMessageHandler(_proofTokenFactory, IdentityServerHost.Server.CreateHandler());
        var tokenClient = new HttpClient(tokenHandler);

        var tokenResponse = await tokenClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = IdentityServerHost.Url("/connect/token"),
            ClientId = "client_credentials_client",
            ClientSecret = "secret",
        }, _ct);

        var apiHandler = new ProofTokenMessageHandler(_proofTokenFactory, ApiHost.Server.CreateHandler());
        var apiClient = new HttpClient(apiHandler);
        apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("DPoP", tokenResponse.AccessToken);

        ApiHost.ApiInvoked += ctx =>
        {
            ctx.User.Identity?.IsAuthenticated.ShouldBeTrue();
        };

        var apiResponse = await apiClient.GetAsync(ApiHost.Url("/api"), _ct);
        apiResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task when_nonce_required_client_assertion_factory_should_be_called_on_retry()
    {
        var capturedAssertions = new List<string>();
        var callCount = 0;
        var nonce = "test-nonce-value";

        var mockInner = new CallbackHttpMessageHandler(async (request, ct) =>
        {
            var body = request.Content != null
                ? await request.Content.ReadAsStringAsync(ct)
                : string.Empty;
            var pairs = HttpUtility.ParseQueryString(body);
            var assertion = pairs["client_assertion"];
            if (assertion != null)
            {
                capturedAssertions.Add(assertion);
            }

            callCount++;
            if (callCount == 1)
            {
                var resp = new HttpResponseMessage(HttpStatusCode.BadRequest);
                resp.Headers.Add("DPoP-Nonce", nonce);
                resp.Content = new StringContent(
                    """{"error":"use_dpop_nonce"}""",
                    System.Text.Encoding.UTF8, "application/json");
                return resp;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"tok","token_type":"DPoP","expires_in":3600}""",
                    System.Text.Encoding.UTF8, "application/json")
            };
        });

        var assertionCallCount = 0;
        var factory = () =>
        {
            assertionCallCount++;
            return Task.FromResult(new ClientAssertion
            {
                Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                Value = $"assertion_{assertionCallCount}"
            });
        };

        var handler = new ProofTokenMessageHandler(_proofTokenFactory, mockInner);
        var client = new HttpClient(handler);

        var initialContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_assertion_type", OidcConstants.ClientAssertionTypes.JwtBearer),
            new KeyValuePair<string, string>("client_assertion", "original_assertion"),
            new KeyValuePair<string, string>("scope", "scope1"),
        });
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://server/connect/token")
        {
            Content = initialContent
        };
        requestMessage.Options.Set(ProtocolRequestOptions.ClientAssertionFactory, factory);

        await client.SendAsync(requestMessage, _ct);

        callCount.ShouldBe(2, "Expected initial request + nonce retry");
        capturedAssertions.Count.ShouldBe(2);
        capturedAssertions[0].ShouldBe("original_assertion",
            "First request should use the original body's assertion");
        capturedAssertions[1].ShouldBe("assertion_1",
            "Retry request should use the fresh assertion from ClientAssertionFactory");
        assertionCallCount.ShouldBe(1, "ClientAssertionFactory should be called exactly once (on retry)");
    }

    [Fact]
    public async Task when_no_client_assertion_factory_nonce_retry_does_not_modify_body()
    {
        var capturedAssertions = new List<string>();
        var callCount = 0;
        var nonce = "test-nonce-backward-compat";

        var mockInner = new CallbackHttpMessageHandler(async (request, ct) =>
        {
            var body = request.Content != null
                ? await request.Content.ReadAsStringAsync(ct)
                : string.Empty;
            var pairs = HttpUtility.ParseQueryString(body);
            var assertion = pairs["client_assertion"];
            if (assertion != null)
            {
                capturedAssertions.Add(assertion);
            }

            callCount++;
            if (callCount == 1)
            {
                var resp = new HttpResponseMessage(HttpStatusCode.BadRequest);
                resp.Headers.Add("DPoP-Nonce", nonce);
                resp.Content = new StringContent(
                    """{"error":"use_dpop_nonce"}""",
                    System.Text.Encoding.UTF8, "application/json");
                return resp;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"tok","token_type":"DPoP","expires_in":3600}""",
                    System.Text.Encoding.UTF8, "application/json")
            };
        });

        // No ClientAssertionFactory set
        var handler = new ProofTokenMessageHandler(_proofTokenFactory, mockInner);
        var client = new HttpClient(handler);

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://server/connect/token")
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_assertion_type", OidcConstants.ClientAssertionTypes.JwtBearer),
                new KeyValuePair<string, string>("client_assertion", "static_assertion"),
            })
        };

        await client.SendAsync(requestMessage, _ct);

        // Both requests should carry the same (unchanged) assertion — backward compatible
        callCount.ShouldBe(2, "Expected initial request + nonce retry");
        capturedAssertions.Count.ShouldBe(2);
        capturedAssertions[0].ShouldBe("static_assertion");
        capturedAssertions[1].ShouldBe("static_assertion",
            "Without ClientAssertionFactory, body must not be modified on retry");
    }

    private sealed class CallbackHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => callback(request, cancellationToken);
    }
}
