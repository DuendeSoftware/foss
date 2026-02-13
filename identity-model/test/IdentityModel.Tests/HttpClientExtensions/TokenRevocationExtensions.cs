// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityModel.Infrastructure;

using Microsoft.AspNetCore.WebUtilities;

namespace Duende.IdentityModel.HttpClientExtensions;

public class TokenRevocationExtensionsTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private const string Endpoint = "http://server/endpoint";

    [Fact]
    public async Task Http_request_should_have_correct_format()
    {
        var handler = new NetworkHandler(HttpStatusCode.NotFound, "not found");

        var client = new HttpClient(handler);
        var request = new TokenRevocationRequest
        {
            Address = Endpoint,
            Token = "token"
        };

        request.Headers.Add("custom", "custom");
        request.GetProperties().Add("custom", "custom");

        var response = await client.RevokeTokenAsync(request, _ct);

        var httpRequest = handler.Request;

        httpRequest.Method.ShouldBe(HttpMethod.Post);
        httpRequest.RequestUri.ShouldBe(new Uri(Endpoint));
        httpRequest.Content.ShouldNotBeNull();

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
    public async Task Valid_protocol_response_should_be_handled_correctly()
    {
        var handler = new NetworkHandler(HttpStatusCode.OK, "ok");
        var client = new HttpClient(handler);

        var response = await client.RevokeTokenAsync(new TokenRevocationRequest
        {
            Address = Endpoint,
            Token = "token",
            ClientId = "client"
        }, _ct);

        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Repeating_a_request_should_succeed()
    {
        var handler = new NetworkHandler(HttpStatusCode.OK, "ok");
        var client = new HttpClient(handler);

        var request = new TokenRevocationRequest
        {
            Address = Endpoint,
            Token = "token",
            ClientId = "client"
        };

        var response = await client.RevokeTokenAsync(request, _ct);

        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);

        // repeat
        response = await client.RevokeTokenAsync(request, _ct);

        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Valid_protocol_error_should_be_handled_correctly()
    {
        var document = File.ReadAllText(FileName.Create("failure_token_revocation_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.BadRequest);
        var client = new HttpClient(handler);

        var response = await client.RevokeTokenAsync(new TokenRevocationRequest
        {
            Address = Endpoint,
            Token = "token",
            ClientId = "client"
        }, _ct);

        response.IsError.ShouldBeTrue();
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Error.ShouldBe("error");
    }

    [Fact]
    public async Task Malformed_response_document_should_be_handled_correctly()
    {
        var document = "invalid";
        var handler = new NetworkHandler(document, HttpStatusCode.BadRequest);
        var client = new HttpClient(handler);

        var response = await client.RevokeTokenAsync(new TokenRevocationRequest
        {
            Address = Endpoint,
            Token = "token",
            ClientId = "client"
        }, _ct);

        response.IsError.ShouldBeTrue();
        response.ErrorType.ShouldBe(ResponseErrorType.Exception);
        response.Raw.ShouldBe("invalid");
        response.Exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task Exception_should_be_handled_correctly()
    {
        var handler = new NetworkHandler(new Exception("exception"));
        var client = new HttpClient(handler);

        var response = await client.RevokeTokenAsync(new TokenRevocationRequest
        {
            Address = Endpoint,
            Token = "token",
            ClientId = "client"
        }, _ct);

        response.IsError.ShouldBeTrue();
        response.ErrorType.ShouldBe(ResponseErrorType.Exception);
        response.Error.ShouldBe("exception");
        response.Exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task Http_error_should_be_handled_correctly()
    {
        var handler = new NetworkHandler(HttpStatusCode.NotFound, "not found");
        var client = new HttpClient(handler);

        var response = await client.RevokeTokenAsync(new TokenRevocationRequest
        {
            Address = Endpoint,
            Token = "token",
            ClientId = "client"
        }, _ct);

        response.IsError.ShouldBeTrue();
        response.ErrorType.ShouldBe(ResponseErrorType.Http);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Error.ShouldBe("not found");
    }

    [Fact]
    public async Task Additional_parameters_should_be_sent_correctly()
    {
        var handler = new NetworkHandler(HttpStatusCode.OK, "ok");
        var client = new HttpClient(handler);

        var response = await client.RevokeTokenAsync(new TokenRevocationRequest
        {
            Address = Endpoint,
            ClientId = "client",
            ClientSecret = "secret",
            Token = "token",
            Parameters =
            {
                { "foo", "bar" }
            }
        }, _ct);

        // check request
        var fields = QueryHelpers.ParseQuery(handler.Body);
        fields.Count.ShouldBe(2);

        fields["token"].First().ShouldBe("token");
        fields["foo"].First().ShouldBe("bar");

        // check response
        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
