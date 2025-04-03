// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.Infrastructure;

namespace Duende.IdentityModel.HttpClientExtensions;

public class TokenIntrospectionTests
{
    private const string Endpoint = "http://server/token";

    [Fact]
    public async Task Http_request_should_have_correct_format()
    {
        var handler = new NetworkHandler(HttpStatusCode.NotFound, "not found");

        var client = new HttpClient(handler);
        var request = new TokenIntrospectionRequest
        {
            Address = Endpoint,
            Token = "token"
        };

        request.Headers.Add("custom", "custom");
        request.GetProperties().Add("custom", "custom");

        _ = await client.IntrospectTokenAsync(request);

        var httpRequest = handler.Request;

        httpRequest.Method.ShouldBe(HttpMethod.Post);
        httpRequest.RequestUri.ShouldBe(new Uri(Endpoint));
        httpRequest.Content.ShouldNotBeNull();
        httpRequest.Headers.Accept.ShouldBe([MediaTypeWithQualityHeaderValue.Parse("application/json")]);
        httpRequest.Headers.GetValues("custom").ShouldBe(["custom"]);
        httpRequest.GetProperties().ShouldBe(new Dictionary<string, object>
        {
            ["custom"] = "custom",
        });
    }

    [Fact]
    public async Task Success_protocol_response_should_be_handled_correctly()
    {
        var document = File.ReadAllText(FileName.Create("success_introspection_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(Endpoint)
        };

        var response = await client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Token = "token"
        });

        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.IsActive.ShouldBeTrue();
        var expected = new Claim[]
        {
            new("aud", "https://idsvr4/resources", ClaimValueTypes.String, "https://idsvr4"),
            new("aud", "api1", ClaimValueTypes.String, "https://idsvr4"),
            new("iss", "https://idsvr4", ClaimValueTypes.String, "https://idsvr4"),
            new("nbf", "1475824871", ClaimValueTypes.String, "https://idsvr4"),
            new("exp", "1475828471", ClaimValueTypes.String, "https://idsvr4"),
            new("client_id", "client", ClaimValueTypes.String, "https://idsvr4"),
            new("sub", "1", ClaimValueTypes.String, "https://idsvr4"),
            new("auth_time", "1475824871", ClaimValueTypes.String, "https://idsvr4"),
            new("idp", "local", ClaimValueTypes.String, "https://idsvr4"),
            new("amr", "password", ClaimValueTypes.String, "https://idsvr4"),
            new("active", "true", ClaimValueTypes.String, "https://idsvr4"),
            new("scope", "api1", ClaimValueTypes.String, "https://idsvr4"),
            new("scope", "api2", ClaimValueTypes.String, "https://idsvr4"),
        };
        response.Claims.ShouldBe(expected, new ClaimComparer());
    }

    [Fact]
    public async Task Success_protocol_response_without_issuer_should_be_handled_correctly()
    {
        var document = File.ReadAllText(FileName.Create("success_introspection_response_no_issuer.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(Endpoint)
        };

        var response = await client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Token = "token"
        });

        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.IsActive.ShouldBeTrue();
        var expectedClaims = new Claim[]
        {
            new("aud", "https://idsvr4/resources", ClaimValueTypes.String, "LOCAL AUTHORITY"),
            new("aud", "api1", ClaimValueTypes.String, "LOCAL AUTHORITY"),
            new("nbf", "1475824871", ClaimValueTypes.String, "LOCAL AUTHORITY"),
            new("exp", "1475828471", ClaimValueTypes.String, "LOCAL AUTHORITY"),
            new("client_id", "client", ClaimValueTypes.String, "LOCAL AUTHORITY"),
            new("sub", "1", ClaimValueTypes.String, "LOCAL AUTHORITY"),
            new("auth_time", "1475824871", ClaimValueTypes.String, "LOCAL AUTHORITY"),
            new("idp", "local", ClaimValueTypes.String, "LOCAL AUTHORITY"),
            new("amr", "password", ClaimValueTypes.String, "LOCAL AUTHORITY"),
            new("active", "true", ClaimValueTypes.String, "LOCAL AUTHORITY"),
            new("scope", "api1", ClaimValueTypes.String, "LOCAL AUTHORITY"),
            new("scope", "api2", ClaimValueTypes.String, "LOCAL AUTHORITY"),
        };
        response.Claims.ShouldBe(expectedClaims, new ClaimComparer());
    }

    [Fact]
    public async Task Repeating_a_request_should_succeed()
    {
        var document = File.ReadAllText(FileName.Create("success_introspection_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(Endpoint)
        };

        var request = new TokenIntrospectionRequest
        {
            Token = "token"
        };

        var response = await client.IntrospectTokenAsync(request);

        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.IsActive.ShouldBeTrue();
        var expectedClaims = new Claim[]
        {
            new("aud", "https://idsvr4/resources", ClaimValueTypes.String, "https://idsvr4"),
            new("aud", "api1", ClaimValueTypes.String, "https://idsvr4"),
            new("iss", "https://idsvr4", ClaimValueTypes.String, "https://idsvr4"),
            new("nbf", "1475824871", ClaimValueTypes.String, "https://idsvr4"),
            new("exp", "1475828471", ClaimValueTypes.String, "https://idsvr4"),
            new("client_id", "client", ClaimValueTypes.String, "https://idsvr4"),
            new("sub", "1", ClaimValueTypes.String, "https://idsvr4"),
            new("auth_time", "1475824871", ClaimValueTypes.String, "https://idsvr4"),
            new("idp", "local", ClaimValueTypes.String, "https://idsvr4"),
            new("amr", "password", ClaimValueTypes.String, "https://idsvr4"),
            new("active", "true", ClaimValueTypes.String, "https://idsvr4"),
            new("scope", "api1", ClaimValueTypes.String, "https://idsvr4"),
            new("scope", "api2", ClaimValueTypes.String, "https://idsvr4"),
        };
        response.Claims.ShouldBe(expectedClaims, new ClaimComparer());

        // repeat
        response = await client.IntrospectTokenAsync(request);

        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.IsActive.ShouldBeTrue();
        response.Claims.ShouldBe(expectedClaims, new ClaimComparer());
    }

    [Fact]
    public async Task Request_without_token_should_fail()
    {
        var document = File.ReadAllText(FileName.Create("success_introspection_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(Endpoint)
        };

        Func<Task> act = async () => await client.IntrospectTokenAsync(new TokenIntrospectionRequest());

        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.Message.ShouldContain("token");
    }

    [Fact]
    public async Task Malformed_response_document_should_be_handled_correctly()
    {
        var document = "invalid";
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler);
        var response = await client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = Endpoint,
            Token = "token"
        });

        response.IsError.ShouldBeTrue();
        response.ErrorType.ShouldBe(ResponseErrorType.Exception);
        response.Raw.ShouldBe("invalid");
        response.Exception.ShouldBeAssignableTo<JsonException>();
    }

    [Fact]
    public async Task Exception_should_be_handled_correctly()
    {
        var exception = new Exception("exception");
        var handler = new NetworkHandler(exception);

        var client = new HttpClient(handler);
        var response = await client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = Endpoint,
            Token = "token"
        });

        response.IsError.ShouldBeTrue();
        response.ErrorType.ShouldBe(ResponseErrorType.Exception);
        response.Error.ShouldBe("exception");
        response.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task Http_error_should_be_handled_correctly()
    {
        var handler = new NetworkHandler(HttpStatusCode.NotFound, "not found");

        var client = new HttpClient(handler);
        var response = await client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = Endpoint,
            Token = "token"
        });

        response.IsError.ShouldBeTrue();
        response.ErrorType.ShouldBe(ResponseErrorType.Http);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Error.ShouldBe("not found");
    }

    [Fact]
    public async Task Legacy_protocol_response_should_be_handled_correctly()
    {
        var document = File.ReadAllText(FileName.Create("legacy_success_introspection_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler);
        var response = await client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = Endpoint,
            Token = "token"
        });

        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.IsActive.ShouldBeTrue();
        var expectedClaims = new Claim[] {
            new("aud", "https://idsvr4/resources", ClaimValueTypes.String, "https://idsvr4"),
            new("aud", "api1", ClaimValueTypes.String, "https://idsvr4"),
            new("iss", "https://idsvr4", ClaimValueTypes.String, "https://idsvr4"),
            new("nbf", "1475824871", ClaimValueTypes.String, "https://idsvr4"),
            new("exp", "1475828471", ClaimValueTypes.String, "https://idsvr4"),
            new("client_id", "client", ClaimValueTypes.String, "https://idsvr4"),
            new("sub", "1", ClaimValueTypes.String, "https://idsvr4"),
            new("auth_time", "1475824871", ClaimValueTypes.String, "https://idsvr4"),
            new("idp", "local", ClaimValueTypes.String, "https://idsvr4"),
            new("amr", "password", ClaimValueTypes.String, "https://idsvr4"),
            new("active", "true", ClaimValueTypes.String, "https://idsvr4"),
            new("scope", "api1", ClaimValueTypes.String, "https://idsvr4"),
            new("scope", "api2", ClaimValueTypes.String, "https://idsvr4")
        };
        response.Claims.ShouldBe(expectedClaims, new ClaimComparer());
    }

    [Fact]
    public async Task Additional_request_parameters_should_be_handled_correctly()
    {
        var document = File.ReadAllText(FileName.Create("success_introspection_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler);
        var response = await client.IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = Endpoint,
            ClientId = "client",
            Token = "token",
            Parameters =
            {
                { "scope", "scope1" },
                { "scope", "scope2" },
                { "foo", "bar baz" }
            }
        });

        // check request
        handler.Body.ShouldBe("scope=scope1&scope=scope2&foo=bar+baz&token=token");

        // check response
        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.IsActive.ShouldBeTrue();
        response.Claims.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Http_request_should_have_correct_accept_header_for_jwt_response()
    {
        var jwtOptions = new IntrospectionClientOptions
        {
            Address = Endpoint,
            ClientId = "client",
            ClientSecret = "secret",
            ResponseFormat = IntrospectionResponseFormat.Jwt
        };

        var handler = new NetworkHandler("{}", HttpStatusCode.OK);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(Endpoint)
        };

        var introspectionClient = new IntrospectionClient(httpClient, jwtOptions);

        _ = await introspectionClient.Introspect("token");

        handler.Request.ShouldNotBeNull();
        var acceptHeaders = handler.Request.Headers.Accept.ToArray();
        acceptHeaders.ShouldBe([MediaTypeWithQualityHeaderValue.Parse($"application/{JwtClaimTypes.JwtTypes.IntrospectionJwtResponse}")]);
    }

    [Fact]
    public async Task Http_request_should_have_correct_accept_header_for_json_response()
    {
        // Leaving the ResponseFormat unset here as default (JSON) should result in the correct Accept header
        var jwtOptions = new IntrospectionClientOptions
        {
            Address = Endpoint,
            ClientId = "client",
            ClientSecret = "secret"
        };

        var handler = new NetworkHandler("{}", HttpStatusCode.OK);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(Endpoint)
        };

        var introspectionClient = new IntrospectionClient(httpClient, jwtOptions);

        _ = await introspectionClient.Introspect("token");

        handler.Request.ShouldNotBeNull();
        var acceptHeaders = handler.Request.Headers.Accept.ToArray();
        acceptHeaders.ShouldBe([MediaTypeWithQualityHeaderValue.Parse("application/json")]);
    }

    [Fact]
    public async Task Jwt_introspection_response_should_be_handled_correctly()
    {
        var document = File.ReadAllText(FileName.Create("success_introspection_response.jwt"));
        var jwtOptions = new IntrospectionClientOptions
        {
            Address = Endpoint,
            ClientId = "client",
            ClientSecret = "secret",
            ResponseFormat = IntrospectionResponseFormat.Jwt
        };

        var handler = new NetworkHandler(document, HttpStatusCode.OK);
        handler.MediaType = $"application/{JwtClaimTypes.JwtTypes.IntrospectionJwtResponse}";

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(Endpoint)
        };

        var introspectionClient = new IntrospectionClient(httpClient, jwtOptions);
        var introspectionResponse = await introspectionClient.Introspect("token");

        introspectionResponse.IsError.ShouldBeFalse();
        introspectionResponse.ErrorType.ShouldBe(ResponseErrorType.None);
        introspectionResponse.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        introspectionResponse.IsActive.ShouldBeTrue();
        introspectionResponse.Raw.ShouldBe(document);

        var expectedClaims = new Claim[]
        {
            new("active", "true", ClaimValueTypes.String, "https://as.example.com/"),
            new("iss", "https://as.example.com/", ClaimValueTypes.String, "https://as.example.com/"),
            new("aud", "https://rs.example.com/resource", ClaimValueTypes.String, "https://as.example.com/"),
            new("iat", "1514797822", ClaimValueTypes.String, "https://as.example.com/"),
            new("exp", "1514797942", ClaimValueTypes.String, "https://as.example.com/"),
            new("client_id", "paiB2goo0a", ClaimValueTypes.String, "https://as.example.com/"),
            new("sub", "Z5O3upPC88QrAjx00dis", ClaimValueTypes.String, "https://as.example.com/"),
            new("birthdate", "1982-02-01", ClaimValueTypes.String, "https://as.example.com/"),
            new("given_name", "John", ClaimValueTypes.String, "https://as.example.com/"),
            new("family_name", "Doe", ClaimValueTypes.String, "https://as.example.com/"),
            new("jti", "t1FoCCaZd4Xv4ORJUWVUeTZfsKhW30CQCrWDDjwXy6w", ClaimValueTypes.String, "https://as.example.com/"),
            new("scope", "read", ClaimValueTypes.String, "https://as.example.com/"),
            new("scope", "write", ClaimValueTypes.String, "https://as.example.com/"),
            new("scope", "dolphin", ClaimValueTypes.String, "https://as.example.com/")
        };

        introspectionResponse.Claims.ShouldNotBeEmpty();
        introspectionResponse.Claims.ShouldBe(expectedClaims, new ClaimComparer());
    }

    [Fact]
    public async Task Json_introspection_response_should_be_handled_correctly()
    {
        var document = File.ReadAllText(FileName.Create("success_introspection_response.json"));
        var jwtOptions = new IntrospectionClientOptions
        {
            Address = Endpoint,
            ClientId = "client",
            ClientSecret = "secret",
            ResponseFormat = IntrospectionResponseFormat.Jwt
        };

        var handler = new NetworkHandler(document, HttpStatusCode.OK);
        handler.MediaType = "application/json";

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(Endpoint)
        };

        var introspectionClient = new IntrospectionClient(httpClient, jwtOptions);

        var introspectionResponse = await introspectionClient.Introspect("token");

        introspectionResponse.IsError.ShouldBeFalse();
        introspectionResponse.ErrorType.ShouldBe(ResponseErrorType.None);
        introspectionResponse.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        introspectionResponse.IsActive.ShouldBeTrue();
        introspectionResponse.Raw.ShouldBe(document);

        var expected = new Claim[]
        {
            new("aud", "https://idsvr4/resources", ClaimValueTypes.String, "https://idsvr4"),
            new("aud", "api1", ClaimValueTypes.String, "https://idsvr4"),
            new("iss", "https://idsvr4", ClaimValueTypes.String, "https://idsvr4"),
            new("nbf", "1475824871", ClaimValueTypes.String, "https://idsvr4"),
            new("exp", "1475828471", ClaimValueTypes.String, "https://idsvr4"),
            new("client_id", "client", ClaimValueTypes.String, "https://idsvr4"),
            new("sub", "1", ClaimValueTypes.String, "https://idsvr4"),
            new("auth_time", "1475824871", ClaimValueTypes.String, "https://idsvr4"),
            new("idp", "local", ClaimValueTypes.String, "https://idsvr4"),
            new("amr", "password", ClaimValueTypes.String, "https://idsvr4"),
            new("active", "true", ClaimValueTypes.String, "https://idsvr4"),
            new("scope", "api1", ClaimValueTypes.String, "https://idsvr4"),
            new("scope", "api2", ClaimValueTypes.String, "https://idsvr4"),
        };

        introspectionResponse.Claims.ShouldNotBeEmpty();
        introspectionResponse.Claims.ShouldBe(expected, new ClaimComparer());
    }
}
