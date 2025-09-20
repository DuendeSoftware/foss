// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityModel.Infrastructure;


namespace Duende.IdentityModel.HttpClientExtensions;

public class UserInfoExtensionsTests
{
    private const string Endpoint = "http://server/endpoint";

    [Fact]
    public async Task Valid_protocol_response_should_be_handled_correctly()
    {
        var document = File.ReadAllText(FileName.Create("success_userinfo_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler);
        var response = await client.GetUserInfoAsync(new UserInfoRequest
        {
            Address = Endpoint,
            Token = "token"
        });

        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.Claims.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Http_request_should_have_correct_format()
    {
        var handler = new NetworkHandler(HttpStatusCode.NotFound, "not found");

        var client = new HttpClient(handler);
        var request = new UserInfoRequest
        {
            Address = Endpoint,
            Token = "token"
        };

        request.Headers.Add("custom", "custom");
        request.GetProperties().Add("custom", "custom");

        var response = await client.GetUserInfoAsync(request);

        var httpRequest = handler.Request;

        httpRequest.Method.ShouldBe(HttpMethod.Get);
        httpRequest.RequestUri.ShouldBe(new Uri(Endpoint));
        httpRequest.Content.ShouldBeNull();

        var headers = httpRequest.Headers;
        headers.Count().ShouldBe(3);
        headers.ShouldContain(h => h.Key == "custom" && h.Value.First() == "custom");
        headers.ShouldContain(h => h.Key == "Authorization" && h.Value.First() == "Bearer token");

        var properties = httpRequest.GetProperties();
        properties.Count.ShouldBe(1);

        var prop = properties.First();
        prop.Key.ShouldBe("custom");
        ((string)prop.Value).ShouldBe("custom");
    }


    [Fact]
    public async Task Malformed_response_document_should_be_handled_correctly()
    {
        var document = "invalid";
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler);
        var response = await client.GetUserInfoAsync(new UserInfoRequest
        {
            Address = Endpoint,
            Token = "token"
        });

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
        var response = await client.GetUserInfoAsync(new UserInfoRequest
        {
            Address = Endpoint,
            Token = "token"
        });

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
        var response = await client.GetUserInfoAsync(new UserInfoRequest
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
    public async Task BadRequest_with_empty_body_should_be_handled_as_error()
    {
        var document = "";
        var handler = new NetworkHandler(document, HttpStatusCode.BadRequest);

        var client = new HttpClient(handler);
        var response = await client.GetUserInfoAsync(new UserInfoRequest
        {
            Address = Endpoint,
            Token = "token"
        });

        response.IsError.ShouldBeTrue();
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
        response.Raw.ShouldBe("");
        response.Error.ShouldBeNull();
        response.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task Non_json_response_should_set_raw()
    {
        var document = File.ReadAllText(FileName.Create("success_userinfo_response.jwt"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK)
        {
            MediaType = "application/jwt"
        };

        var client = new HttpClient(handler);
        var response = await client.GetUserInfoAsync(new UserInfoRequest
        {
            Address = Endpoint,
            Token = "token"
        });

        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.Claims.ShouldBeEmpty();

        // This is just the literal content of the success_userinfo_response.jwt
        var expectedContent = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJodHRwczovL2lkZW50aXR5LmV4YW1wbGUuY29tIiwiYXVkIjoiaHR0cHM6Ly9hcHAuZXhhbXBsZS5jb20iLCJzdWIiOiIyNDgyODk3NjEwMDEiLCJuYW1lIjoiSmFuZSBEb2UiLCJnaXZlbl9uYW1lIjoiSmFuZSIsImZhbWlseV9uYW1lIjoiRG9lIiwicHJlZmVycmVkX3VzZXJuYW1lIjoiai5kb2UiLCJlbWFpbCI6ImphbmVkb2VAZXhhbXBsZS5jb20iLCJwaWN0dXJlIjoiaHR0cDovL2V4YW1wbGUuY29tL2phbmVkb2UvbWUuanBnIn0.WmamfT6SSfVrJ6iBqPprRvbjKlQpd_8OcjLSbKbfMTQ";
        response.Raw.ShouldBe(expectedContent);
    }

    [Fact]
    public async Task Request_without_body_content_should_use_GET()
    {
        var document = await File.ReadAllTextAsync(FileName.Create("success_userinfo_response.jwt"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK)
        {
            MediaType = "application/jwt"
        };

        var client = new HttpClient(handler);
        var response = await client.GetUserInfoAsync(new UserInfoRequest
        {
            Address = Endpoint,
            Token = "token"
        });

        var httpRequest = handler.Request;

        httpRequest.Method.ShouldBe(HttpMethod.Get);
        httpRequest.RequestUri.ShouldBe(new Uri(Endpoint));
        httpRequest.Content.ShouldBeNull();
    }

    [Fact]
    public async Task Request_with_body_content_should_use_POST()
    {
        var document = await File.ReadAllTextAsync(FileName.Create("success_userinfo_response.jwt"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK)
        {
            MediaType = "application/jwt"
        };

        var client = new HttpClient(handler);
        var response = await client.GetUserInfoAsync(new UserInfoRequest
        {
            Address = Endpoint,
            Token = "token",
            ClientAssertion = new ClientAssertion
            {
                Type = "test",
                Value = "value"
            }
        });

        var httpRequest = handler.Request;

        httpRequest.Method.ShouldBe(HttpMethod.Post);
        httpRequest.RequestUri.ShouldBe(new Uri(Endpoint));
        httpRequest.Content.ShouldNotBeNull();
    }
}
