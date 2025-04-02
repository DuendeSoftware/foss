// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.Infrastructure;


namespace Duende.IdentityModel.HttpClientExtensions;

public class DiscoveryExtensionsTests
{
    private readonly NetworkHandler _successHandler;
    private readonly string _endpoint = "https://demo.identityserver.io/.well-known/openid-configuration";
    private readonly string _authority = "https://demo.identityserver.io";

    public DiscoveryExtensionsTests()
    {
        var discoFileName = FileName.Create("discovery.json");
        var document = File.ReadAllText(discoFileName);

        var jwksFileName = FileName.Create("discovery_jwks.json");
        var jwks = File.ReadAllText(jwksFileName);

        _successHandler = new NetworkHandler(request =>
        {
            if (request.RequestUri.AbsoluteUri.EndsWith("jwks"))
            {
                return jwks;
            }

            return document;
        }, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Http_request_should_have_correct_format()
    {
        var handler = new NetworkHandler(HttpStatusCode.NotFound, "not found");

        var client = new HttpClient(handler);
        var request = new DiscoveryDocumentRequest
        {
            Address = _endpoint
        };

        request.Headers.Add("custom", "custom");
        request.GetProperties().Add("custom", "custom");

        var response = await client.GetDiscoveryDocumentAsync(request);

        var httpRequest = handler.Request;

        httpRequest.Method.ShouldBe(HttpMethod.Get);
        httpRequest.RequestUri.ShouldBe(new Uri(_endpoint));
        httpRequest.Content.ShouldBeNull();

        var headers = httpRequest.Headers;
        headers.Count().ShouldBe(2);
        headers.ShouldContain(h => h.Key == "custom" && h.Value.First() == "custom");

        var properties = httpRequest.GetProperties();
        properties.Count.ShouldBe(1);

        var prop = properties.First();
        prop.Key.ShouldBe("custom");
        ((string)prop.Value).ShouldBe("custom");
    }


    [Fact]
    public async Task Base_address_should_work()
    {
        var client = new HttpClient(_successHandler)
        {
            BaseAddress = new Uri(_endpoint)
        };

        var disco = await client.GetDiscoveryDocumentAsync();

        disco.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task Null_client_base_address_should_throw()
    {
        var client = new HttpClient(_successHandler) { BaseAddress = null };

        Func<Task> act = () => client.GetDiscoveryDocumentAsync();

        await act.ShouldThrowAsync<ArgumentException>("Either the address parameter or the HttpClient BaseAddress must not be null.");
    }

    [Fact]
    public async Task Null_discovery_document_address_should_throw()
    {
        var client = new HttpClient(_successHandler) { BaseAddress = null };

        Func<Task> act = () => client.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest());

        await act.ShouldThrowAsync<ArgumentException>("Either the DiscoveryDocumentRequest Address or the HttpClient BaseAddress must not be null.");
    }

    [Fact]
    public async Task Explicit_address_should_work()
    {
        var client = new HttpClient(_successHandler);

        var disco = await client.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
        {
            Address = _endpoint
        });

        disco.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task Authority_should_expand_to_endpoint()
    {
        var handler = new NetworkHandler(HttpStatusCode.NotFound, "not found");
        var client = new HttpClient(handler);

        var disco = await client.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
        {
            Address = _authority
        });

        disco.IsError.ShouldBeTrue();
        handler.Request.RequestUri!.AbsoluteUri.ShouldBe(_endpoint);
    }

    [Fact]
    public async Task Http_error_should_be_handled_correctly()
    {
        var handler = new NetworkHandler(HttpStatusCode.NotFound, "not found");
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(_endpoint)
        };

        var disco = await client.GetDiscoveryDocumentAsync();

        disco.IsError.ShouldBeTrue();
        disco.ErrorType.ShouldBe(ResponseErrorType.Http);
        disco.Error.ShouldStartWith("Error connecting to");
        disco.Error.ShouldEndWith("not found");
        disco.HttpStatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Policy_authority_does_not_get_overwritten()
    {
        var policy = new DiscoveryPolicy
        {
            Authority = "https://server:123"
        };

        var client = new HttpClient(_successHandler);
        var disco = await client.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
        {
            Address = _endpoint,
            Policy = policy
        });

        disco.IsError.ShouldBeTrue();
        policy.Authority.ShouldBe("https://server:123");
    }

    [Fact]
    public async Task Exception_should_be_handled_correctly()
    {
        var handler = new NetworkHandler(new Exception("error"));

        var client = new HttpClient(handler);
        var disco = await client.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
        {
            Address = _endpoint
        });

        disco.IsError.ShouldBeTrue();
        disco.ErrorType.ShouldBe(ResponseErrorType.Exception);
        disco.Error.ShouldStartWith("Error connecting to");
        disco.Error.ShouldEndWith("error.");
    }

    [Fact]
    public async Task TryGetValue_calls_should_behave_as_excected()
    {
        var client = new HttpClient(_successHandler)
        {
            BaseAddress = new Uri(_endpoint)
        };

        var disco = await client.GetDiscoveryDocumentAsync();

        disco.IsError.ShouldBeFalse();

        disco.TryGetValue(OidcConstants.Discovery.AuthorizationEndpoint).ShouldNotBeNull();
        disco.TryGetValue("unknown")?.ValueKind.ShouldBe(JsonValueKind.Undefined);

        disco.TryGetString(OidcConstants.Discovery.AuthorizationEndpoint).ShouldBe("https://demo.identityserver.io/connect/authorize");
        disco.TryGetString("unknown").ShouldBeNull();
    }

    [Fact]
    public async Task Strongly_typed_accessors_should_behave_as_expected()
    {
        var client = new HttpClient(_successHandler)
        {
            BaseAddress = new Uri(_endpoint)
        };

        var disco = await client.GetDiscoveryDocumentAsync();

        disco.IsError.ShouldBeFalse();

        disco.TokenEndpoint.ShouldBe("https://demo.identityserver.io/connect/token");
        disco.AuthorizeEndpoint.ShouldBe("https://demo.identityserver.io/connect/authorize");
        disco.UserInfoEndpoint.ShouldBe("https://demo.identityserver.io/connect/userinfo");
        disco.PushedAuthorizationRequestEndpoint.ShouldBe("https://demo.identityserver.io/connect/par");

        disco.FrontChannelLogoutSupported.ShouldBe(true);
        disco.FrontChannelLogoutSessionSupported.ShouldBe(true);

        var responseModes = disco.ResponseModesSupported.ToList();

        responseModes.ShouldContain("form_post");
        responseModes.ShouldContain("query");
        responseModes.ShouldContain("fragment");

        disco.KeySet.Keys.Count.ShouldBe(1);
        disco.KeySet.Keys.First().Kid.ShouldBe("a3rMUgMFv9tPclLa6yF3zAkfquE");

        disco.MtlsEndpointAliases.ShouldNotBeNull();
        disco.MtlsEndpointAliases.TokenEndpoint.ShouldBeNull();

        disco.RequirePushedAuthorizationRequests!.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task Mtls_alias_accessors_should_behave_as_expected()
    {
        var discoFileName = FileName.Create("discovery_mtls.json");
        var document = File.ReadAllText(discoFileName);

        var jwksFileName = FileName.Create("discovery_jwks.json");
        var jwks = File.ReadAllText(jwksFileName);

        var handler = new NetworkHandler(request =>
        {
            if (request.RequestUri.AbsoluteUri.EndsWith("jwks"))
            {
                return jwks;
            }

            return document;
        }, HttpStatusCode.OK);

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(_endpoint)
        };

        var disco = await client.GetDiscoveryDocumentAsync();

        disco.IsError.ShouldBeFalse();
        disco.MtlsEndpointAliases.ShouldNotBeNull();

        disco.MtlsEndpointAliases.TokenEndpoint.ShouldBe("https://mtls.identityserver.io/connect/token");
        disco.MtlsEndpointAliases.Json?.TryGetString(OidcConstants.Discovery.TokenEndpoint).ShouldBe("https://mtls.identityserver.io/connect/token");

        disco.MtlsEndpointAliases.RevocationEndpoint.ShouldBe("https://mtls.identityserver.io/connect/revocation");
        disco.MtlsEndpointAliases.IntrospectionEndpoint.ShouldBe("https://mtls.identityserver.io/connect/introspect");
        disco.MtlsEndpointAliases.DeviceAuthorizationEndpoint.ShouldBe("https://mtls.identityserver.io/connect/deviceauthorization");
    }

    [Fact]
    public async Task Http_error_with_non_json_content_should_be_handled_correctly()
    {
        var handler = new NetworkHandler("not_json", HttpStatusCode.InternalServerError);
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(_endpoint)
        };

        var disco = await client.GetDiscoveryDocumentAsync();

        disco.IsError.ShouldBeTrue();
        disco.ErrorType.ShouldBe(ResponseErrorType.Http);
        disco.HttpStatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        disco.Error.ShouldContain("Internal Server Error");
        disco.Raw.ShouldBe("not_json");
        disco.Json?.ValueKind.ShouldBe(JsonValueKind.Undefined);
    }

    [Fact]
    public async Task Http_error_with_json_content_should_be_handled_correctly()
    {
        var content = new
        {
            foo = "foo",
            bar = "bar"
        };

        var handler = new NetworkHandler(JsonSerializer.Serialize(content), HttpStatusCode.InternalServerError);

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(_endpoint)
        };

        var disco = await client.GetDiscoveryDocumentAsync();

        disco.IsError.ShouldBeTrue();
        disco.ErrorType.ShouldBe(ResponseErrorType.Http);
        disco.HttpStatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        disco.Error.ShouldContain("Internal Server Error");

        disco.Json?.TryGetString("foo").ShouldBe("foo");
        disco.Json?.TryGetString("bar").ShouldBe("bar");
    }

    [Fact]
    public async Task Http_error_at_jwk_with_non_json_content_should_be_handled_correctly()
    {
        var handler = new NetworkHandler(request =>
        {
            HttpResponseMessage response;

            if (!request.RequestUri.AbsoluteUri.Contains("jwk"))
            {
                var discoFileName = FileName.Create("discovery.json");
                var document = File.ReadAllText(discoFileName);

                response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(document)
                };
            }
            else
            {
                response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("not_json")
                };
            }

            return response;
        });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(_endpoint)
        };

        var disco = await client.GetDiscoveryDocumentAsync();

        disco.IsError.ShouldBeTrue();
        disco.ErrorType.ShouldBe(ResponseErrorType.Http);
        disco.HttpStatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        disco.Error.ShouldContain("Internal Server Error");
        disco.Raw.ShouldBe("not_json");
        disco.Json?.ValueKind.ShouldBe(JsonValueKind.Undefined);
    }

    [Fact]
    public async Task Http_error_at_jwk_with_json_content_should_be_handled_correctly()
    {
        var handler = new NetworkHandler(request =>
        {
            HttpResponseMessage response;

            if (!request.RequestUri.AbsoluteUri.Contains("jwk"))
            {
                var discoFileName = FileName.Create("discovery.json");
                var document = File.ReadAllText(discoFileName);

                response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(document)
                };
            }
            else
            {
                var content = new
                {
                    foo = "foo",
                    bar = "bar"
                };

                response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(JsonSerializer.Serialize(content))
                };
            }

            return response;
        });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(_endpoint)
        };

        var disco = await client.GetDiscoveryDocumentAsync();

        disco.IsError.ShouldBeTrue();
        disco.ErrorType.ShouldBe(ResponseErrorType.Http);
        disco.HttpStatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        disco.Error.ShouldContain("Internal Server Error");

        disco.Json?.TryGetString("foo").ShouldBe("foo");
        disco.Json?.TryGetString("bar").ShouldBe("bar");
    }

    [Fact]
    public async Task Exception_at_jwk_should_be_handled_correctly()
    {
        var handler = new NetworkHandler(request =>
        {
            if (!request.RequestUri.AbsoluteUri.Contains("jwk"))
            {
                var discoFileName = FileName.Create("discovery.json");
                var document = File.ReadAllText(discoFileName);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(document)
                };
            }
            else
            {
                throw new Exception("jwk failure");
            }
        });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(_endpoint)
        };

        var disco = await client.GetDiscoveryDocumentAsync();

        disco.IsError.ShouldBeTrue();
        disco.ErrorType.ShouldBe(ResponseErrorType.Exception);
        disco.Error.ShouldContain("jwk failure");
        disco.Error.ShouldNotContain("Object reference not set to an instance of an object");
    }

    [Fact]
    public async Task Http_error_at_jwk_with_no_content_should_be_handled_correctly()
    {
        var handler = new NetworkHandler(request =>
        {
            HttpResponseMessage response;

            if (!request.RequestUri.AbsoluteUri.Contains("jwk"))
            {
                var discoFileName = FileName.Create("discovery.json");
                var document = File.ReadAllText(discoFileName);

                response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(document)
                };
            }
            else
            {
                response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return response;
        });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(_endpoint)
        };

        var disco = await client.GetDiscoveryDocumentAsync();

        disco.IsError.ShouldBeTrue();
        disco.ErrorType.ShouldBe(ResponseErrorType.Http);
        disco.HttpStatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        disco.Error.ShouldContain("Internal Server Error");
        disco.Json?.ValueKind.ShouldBe(JsonValueKind.Undefined);
    }
}
