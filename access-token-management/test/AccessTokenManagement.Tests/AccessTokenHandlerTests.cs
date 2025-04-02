// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.Metrics;
using Duende.AccessTokenManagement.OTel;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement.Tests;

public class AccessTokenHandlerTests
{
    TestDPoPProofService _testDPoPProofService = new TestDPoPProofService();
    TestHttpMessageHandler _testHttpMessageHandler = new TestHttpMessageHandler();

    AccessTokenHandlerSubject _subject;

    public AccessTokenHandlerTests(ITestOutputHelper output)
    {
        _subject = new AccessTokenHandlerSubject(_testDPoPProofService, new TestDPoPNonceStore(), new TestLoggerProvider(output.WriteLine, "AccessTokenHandler").CreateLogger("AccessTokenHandlerSubject"));
        _subject.InnerHandler = _testHttpMessageHandler;
    }

    [Fact]
    public async Task lower_case_token_type_should_be_converted_to_case_sensitive()
    {
        var client = new HttpClient(_subject);

        {
            _subject.AccessToken.AccessTokenType = "bearer";

            var response = await client.GetAsync("https://test/api");

            _testHttpMessageHandler.Request!.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        }

        {
            _subject.AccessToken.AccessTokenType = "dpop";

            var response = await client.GetAsync("https://test/api");

            _testHttpMessageHandler.Request!.Headers.Authorization!.Scheme.ShouldBe("DPoP");
        }
    }

    public class TestHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; set; }
        public HttpResponseMessage Response { get; set; } = new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Response);
        }
    }

    public class AccessTokenHandlerSubject(
        IDPoPProofService dPoPProofService,
        IDPoPNonceStore dPoPNonceStore,
        ILogger logger)
        : AccessTokenHandler(new AccessTokenManagementMetrics(new DummyMeterFactory()), dPoPProofService, dPoPNonceStore, logger)
    {
        public ClientCredentialsToken AccessToken { get; set; } = new ClientCredentialsToken
        {
            AccessToken = "at",
            AccessTokenType = "bearer",
            ClientId = "some-client"
        };

        protected override Task<ClientCredentialsToken> GetAccessTokenAsync(bool forceRenewal, CancellationToken cancellationToken)
        {
            return Task.FromResult(AccessToken);
        }

        protected override AccessTokenManagementMetrics.TokenRequestType TokenRequestType => AccessTokenManagementMetrics.TokenRequestType.ClientCredentials;

        private class DummyMeterFactory : IMeterFactory
        {
            public void Dispose()
            {
            }

            public Meter Create(MeterOptions options)
            {
                return new Meter(options);
            }
        }
    }


}
