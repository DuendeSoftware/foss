// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityModel.Infrastructure;

using Microsoft.AspNetCore.WebUtilities;

namespace Duende.IdentityModel.HttpClientExtensions;

public class PushedAuthorizationTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private const string Endpoint = "http://server/par";

    private PushedAuthorizationRequest Request = new PushedAuthorizationRequest
    {
        ClientId = "client",
        ResponseType = "code",
        Address = Endpoint
    };

    [Fact]
    public async Task Http_request_should_have_correct_format()
    {
        var handler = new NetworkHandler(HttpStatusCode.NotFound, "not found");

        var client = new HttpClient(handler);
        var request = new PushedAuthorizationRequest
        {
            ClientId = "client",
            ResponseType = "code",
            Address = Endpoint,
            RedirectUri = "https://example.com/signin-oidc",
            Scope = "openid profile",
            Nonce = "1234",
            State = "5678"
        };
        request.Headers.Add("custom", "custom");
        request.GetProperties().Add("custom", "custom");

        var response = await client.PushAuthorizationAsync(request, _ct);

        var httpRequest = handler.Request;

        httpRequest.Method.ShouldBe(HttpMethod.Post);
        httpRequest.RequestUri.ShouldBe(new Uri(Endpoint));
        httpRequest.Content.ShouldNotBeNull();

        var headers = httpRequest.Headers;
        headers.Count().ShouldBe(3);
        headers.ShouldContain(h => h.Key == "custom" && h.Value.First() == "custom");

        var properties = httpRequest.GetProperties();
        properties.Count.ShouldBe(1);

        var prop = properties.First();
        prop.Key.ShouldBe("custom");
        ((string)prop.Value).ShouldBe("custom");
    }

    [Fact]
    public async Task Request_with_request_object_should_succeed()
    {
        var document = File.ReadAllText(FileName.Create("success_par_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler);
        var request = new PushedAuthorizationRequest
        {
            ClientId = "client",
            Request = "request object value",
            Address = Endpoint,
        };

        await client.PushAuthorizationAsync(request, _ct);


        var fields = QueryHelpers.ParseQuery(handler.Body);
        fields.Count.ShouldBe(2);

        fields["client_id"].First().ShouldBe("client");
        fields["request"].First().ShouldBe("request object value");
    }

    [Fact]
    public async Task Success_protocol_response_should_be_handled_correctly()
    {
        var document = File.ReadAllText(FileName.Create("success_par_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(Endpoint)
        };

        var response = await client.PushAuthorizationAsync(Request, _ct);

        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.RequestUri.ShouldBe("urn:ietf:params:oauth:request_uri:123456");
        response.ExpiresIn.ShouldBe(600);
    }

    [Fact]
    public async Task Malformed_response_document_should_be_handled_correctly()
    {
        var document = "invalid";
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler);
        var response = await client.PushAuthorizationAsync(Request, _ct);

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
        var response = await client.PushAuthorizationAsync(Request, _ct);

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
        var response = await client.PushAuthorizationAsync(Request, _ct);

        response.IsError.ShouldBeTrue();
        response.Error.ShouldBe("not found");
        response.ErrorType.ShouldBe(ResponseErrorType.Http);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Additional_request_parameters_should_be_handled_correctly()
    {
        var document = File.ReadAllText(FileName.Create("success_par_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler);
        var response = await client.PushAuthorizationAsync(new PushedAuthorizationRequest
        {
            ClientId = "client",
            ResponseType = "code",
            Address = Endpoint,
            AcrValues = "idp:example",
            Scope = "scope1 scope2",
            Parameters =
            {
                { "foo", "bar" }
            }
        }, _ct);

        // check request
        var fields = QueryHelpers.ParseQuery(handler.Body);
        fields.Count.ShouldBe(5);

        fields["client_id"].First().ShouldBe("client");
        fields["response_type"].First().ShouldBe("code");
        fields["acr_values"].First().ShouldBe("idp:example");
        fields["scope"].First().ShouldBe("scope1 scope2");
        fields["foo"].First().ShouldBe("bar");

        // check response
        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Pushed_authorization_without_response_type_should_fail()
    {
        var document = File.ReadAllText(FileName.Create("success_par_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);
        var client = new HttpClient(handler);

        Request.ResponseType = null;

        var act = async () => await client.PushAuthorizationAsync(Request, _ct);

        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.ParamName.ShouldBe("response_type");
    }

    [Fact]
    public async Task Pushed_authorization_with_request_uri_should_fail()
    {
        var document = File.ReadAllText(FileName.Create("success_par_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);
        var client = new HttpClient(handler);

        Request.Parameters.Add(OidcConstants.AuthorizeRequest.RequestUri, "not allowed");


        var act = async () => await client.PushAuthorizationAsync(Request, _ct);

        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.ParamName.ShouldBe("request_uri");
    }
}
