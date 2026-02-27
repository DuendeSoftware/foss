// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Web;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient.DPoP;

#nullable enable

namespace Duende.IdentityModel.OidcClient;

/// <summary>
/// Unit tests for <see cref="ProofTokenMessageHandler"/> focusing on client assertion
/// refresh behavior during DPoP nonce retries.
/// </summary>
public class ProofTokenMessageHandlerTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    // A stable DPoP nonce returned on the 401, causing the handler to retry.
    private const string ServerNonce = "server-issued-nonce";

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Minimal <see cref="IDPoPProofTokenFactory"/> stub — just returns a fixed proof token.
    /// </summary>
    private sealed class StubDPoPProofTokenFactory : IDPoPProofTokenFactory
    {
        public DPoPProof CreateProofToken(DPoPProofRequest request) =>
            new DPoPProof { ProofToken = "stub-dpop-proof" };
    }

    /// <summary>
    /// Builds a simple form-encoded <see cref="HttpRequestMessage"/> that resembles
    /// a token endpoint request, optionally including a client assertion, and
    /// optionally stores a <see cref="ClientAssertionFactory"/> on the request options.
    /// </summary>
    private static HttpRequestMessage BuildTokenRequest(
        string? assertionType,
        string? assertionValue,
        Func<Task<ClientAssertion>>? factory = null)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "client_credentials"),
            new("client_id", "test_client"),
        };

        if (assertionType != null && assertionValue != null)
        {
            parameters.Add(new(OidcConstants.TokenRequest.ClientAssertionType, assertionType));
            parameters.Add(new(OidcConstants.TokenRequest.ClientAssertion, assertionValue));
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "https://server/connect/token")
        {
            Content = new FormUrlEncodedContent(parameters)
        };

        if (factory != null)
        {
            request.Options.Set(ProtocolRequestOptions.ClientAssertionFactory, factory);
        }

        return request;
    }

    /// <summary>
    /// Creates a delegating-handler chain:
    /// <c>ProofTokenMessageHandler → inner</c>
    /// where <c>inner</c> is driven by the supplied <paramref name="innerHandler"/>.
    /// </summary>
    private static HttpClient BuildClient(HttpMessageHandler innerHandler)
    {
        var handler = new ProofTokenMessageHandler(new StubDPoPProofTokenFactory(), innerHandler);
        return new HttpClient(handler);
    }

    /// <summary>
    /// Returns an inner handler that first responds with a 401 carrying a
    /// <c>DPoP-Nonce</c> header, then responds with a 200 on the retry.
    /// The captured body of the retry request is stored in <paramref name="retryBody"/>.
    /// </summary>
    private static HttpMessageHandler MakeNonceRequiredHandler(out Func<string?> retryBody)
    {
        string? capturedBody = null;
        retryBody = () => capturedBody;

        var callCount = 0;
        return new CallbackHandler(async request =>
        {
            callCount++;
            if (callCount == 1)
            {
                // First call: respond 401 + DPoP-Nonce header so the handler retries.
                var r = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                r.Headers.Add(OidcConstants.HttpHeaders.DPoPNonce, ServerNonce);
                return r;
            }

            // Second call (retry): capture the body and return 200.
            capturedBody = request.Content != null
                ? await request.Content.ReadAsStringAsync()
                : null;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"at\",\"token_type\":\"DPoP\",\"expires_in\":3600}")
            };
        });
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task When_factory_set_and_nonce_required_retry_uses_fresh_client_assertion()
    {
        var factoryCallCount = 0;
        Func<Task<ClientAssertion>> factory = () =>
        {
            factoryCallCount++;
            return Task.FromResult(new ClientAssertion
            {
                Type = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
                Value = $"fresh_jwt_{factoryCallCount}"
            });
        };

        var inner = MakeNonceRequiredHandler(out var getRetryBody);
        using var client = BuildClient(inner);

        using var request = BuildTokenRequest(
            assertionType: "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            assertionValue: "original_jwt",
            factory: factory);

        var response = await client.SendAsync(request, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The factory should have been called exactly once (on the retry).
        factoryCallCount.ShouldBe(1);

        // The retry body should contain the fresh assertion, not the original one.
        var retryFields = HttpUtility.ParseQueryString(getRetryBody()!);
        retryFields[OidcConstants.TokenRequest.ClientAssertion].ShouldBe("fresh_jwt_1");
        retryFields[OidcConstants.TokenRequest.ClientAssertionType]
            .ShouldBe("urn:ietf:params:oauth:client-assertion-type:jwt-bearer");
    }

    [Fact]
    public async Task When_factory_set_and_nonce_required_original_assertion_is_not_reused()
    {
        Func<Task<ClientAssertion>> factory = () => Task.FromResult(new ClientAssertion
        {
            Type = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            Value = "fresh_jwt"
        });

        var inner = MakeNonceRequiredHandler(out var getRetryBody);
        using var client = BuildClient(inner);

        using var request = BuildTokenRequest(
            assertionType: "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            assertionValue: "original_jwt",
            factory: factory);

        await client.SendAsync(request, _ct);

        // The retry must NOT contain the original JWT.
        var retryFields = HttpUtility.ParseQueryString(getRetryBody()!);
        retryFields[OidcConstants.TokenRequest.ClientAssertion].ShouldNotBe("original_jwt");
    }

    [Fact]
    public async Task When_no_factory_and_nonce_required_retry_reuses_original_body_unchanged()
    {
        // Backward-compatibility: if no factory is present the handler retries without
        // touching the form body (same behaviour as before this fix was introduced).
        var inner = MakeNonceRequiredHandler(out var getRetryBody);
        using var client = BuildClient(inner);

        using var request = BuildTokenRequest(
            assertionType: "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            assertionValue: "original_jwt",
            factory: null);   // ← no factory

        var response = await client.SendAsync(request, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Body on retry should still have the original assertion value.
        var retryFields = HttpUtility.ParseQueryString(getRetryBody()!);
        retryFields[OidcConstants.TokenRequest.ClientAssertion].ShouldBe("original_jwt");
    }

    [Fact]
    public async Task When_factory_set_and_no_nonce_required_factory_is_not_invoked()
    {
        // If the server does not return a DPoP nonce there is no retry, so the factory
        // must not be called.
        var factoryCallCount = 0;
        Func<Task<ClientAssertion>> factory = () =>
        {
            factoryCallCount++;
            return Task.FromResult(new ClientAssertion
            {
                Type = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
                Value = "fresh_jwt"
            });
        };

        // Inner handler returns success on first call (no nonce header).
        var inner = new CallbackHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"access_token\":\"at\",\"token_type\":\"DPoP\",\"expires_in\":3600}")
        }));

        using var client = BuildClient(inner);
        using var request = BuildTokenRequest(
            assertionType: "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            assertionValue: "original_jwt",
            factory: factory);

        await client.SendAsync(request, _ct);

        factoryCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task When_factory_returns_null_value_body_is_not_modified()
    {
        // If the factory returns a ClientAssertion with a null Value the handler should
        // leave the form body as-is (defensive fallback).
        Func<Task<ClientAssertion>> factory = () =>
            Task.FromResult<ClientAssertion>(new ClientAssertion { Value = null! });

        var inner = MakeNonceRequiredHandler(out var getRetryBody);
        using var client = BuildClient(inner);

        using var request = BuildTokenRequest(
            assertionType: "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            assertionValue: "original_jwt",
            factory: factory);

        var response = await client.SendAsync(request, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Body should be unchanged when factory returns a null value.
        var retryFields = HttpUtility.ParseQueryString(getRetryBody()!);
        retryFields[OidcConstants.TokenRequest.ClientAssertion].ShouldBe("original_jwt");
    }

    [Fact]
    public async Task When_factory_set_duplicate_form_keys_are_preserved()
    {
        // Requests with multiple "resource" (or other) parameters must not lose values
        // when the body is re-built after refreshing the client assertion.
        Func<Task<ClientAssertion>> factory = () => Task.FromResult(new ClientAssertion
        {
            Type = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            Value = "fresh_jwt"
        });

        var inner = MakeNonceRequiredHandler(out var getRetryBody);
        using var client = BuildClient(inner);

        // Manually build a request body with duplicate "resource" keys.
        var pairs = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "client_credentials"),
            new("client_id", "test_client"),
            new("resource", "urn:resource:a"),
            new("resource", "urn:resource:b"),
            new(OidcConstants.TokenRequest.ClientAssertionType, "urn:ietf:params:oauth:client-assertion-type:jwt-bearer"),
            new(OidcConstants.TokenRequest.ClientAssertion, "original_jwt"),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://server/connect/token")
        {
            Content = new FormUrlEncodedContent(pairs)
        };
        request.Options.Set(ProtocolRequestOptions.ClientAssertionFactory, factory);

        await client.SendAsync(request, _ct);

        // Rebuild as NameValueCollection (same as the handler does internally via
        // HttpUtility.ParseQueryString) to check multi-values.
        var retryFields = HttpUtility.ParseQueryString(getRetryBody()!);
        var resourceValues = retryFields.GetValues("resource");
        resourceValues.ShouldNotBeNull();
        resourceValues.ShouldContain("urn:resource:a");
        resourceValues.ShouldContain("urn:resource:b");

        // And the assertion should be refreshed.
        retryFields[OidcConstants.TokenRequest.ClientAssertion].ShouldBe("fresh_jwt");
    }

    // ---------------------------------------------------------------------------
    // Private helper: a simple delegate-based HttpMessageHandler
    // ---------------------------------------------------------------------------

    private sealed class CallbackHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _callback;

        public CallbackHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) =>
            _callback = callback;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            _callback(request);
    }
}
