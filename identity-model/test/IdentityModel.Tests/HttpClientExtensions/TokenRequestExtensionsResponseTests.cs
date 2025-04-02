// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.Infrastructure;


namespace Duende.IdentityModel.HttpClientExtensions;

public class TokenRequestExtensionsResponseTests
{
    private const string Endpoint = "http://server/token";

    [Fact]
    public async Task Valid_protocol_response_should_be_handled_correctly()
    {
        var document = File.ReadAllText(FileName.Create("success_token_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler);
        var response = await client.RequestTokenAsync(new TokenRequest
        {
            Address = Endpoint,
            GrantType = "test",
            ClientId = "client"
        });

        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.ExpiresIn.ShouldBe(3600);
        response.AccessToken.ShouldBe("access_token");
        response.RefreshToken.ShouldBe("refresh_token");
        response.TryGet("custom").ShouldBe("custom");
    }

    [Fact]
    public async Task Valid_protocol_error_should_be_handled_correctly()
    {
        var document = File.ReadAllText(FileName.Create("failure_token_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.BadRequest);

        var client = new HttpClient(handler);
        var response = await client.RequestTokenAsync(new TokenRequest
        {
            Address = Endpoint,
            GrantType = "test",
            ClientId = "client"
        });

        response.IsError.ShouldBeTrue();
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Error.ShouldBe("error");
        response.ErrorDescription.ShouldBe("error_description");
        response.TryGet("custom").ShouldBe("custom");
    }

    [Fact]
    public async Task Malformed_response_document_should_be_handled_correctly()
    {
        var document = "invalid";
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler);
        var response = await client.RequestTokenAsync(new TokenRequest
        {
            Address = Endpoint,
            GrantType = "test",
            ClientId = "client"
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
        var response = await client.RequestTokenAsync(new TokenRequest
        {
            Address = Endpoint,
            GrantType = "test",
            ClientId = "client"
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
        var response = await client.RequestTokenAsync(new TokenRequest
        {
            Address = Endpoint,
            GrantType = "test",
            ClientId = "client"
        });

        response.IsError.ShouldBeTrue();
        response.ErrorType.ShouldBe(ResponseErrorType.Http);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Error.ShouldBe("not found");
    }

    [Fact]
    public async Task Http_error_with_non_json_content_should_be_handled_correctly()
    {
        var handler = new NetworkHandler("not_json", HttpStatusCode.Unauthorized);

        var client = new HttpClient(handler);
        var response = await client.RequestTokenAsync(new TokenRequest
        {
            Address = Endpoint,
            GrantType = "test",
            ClientId = "client"
        });

        response.IsError.ShouldBeTrue();
        response.ErrorType.ShouldBe(ResponseErrorType.Http);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Error.ShouldBe("Unauthorized");
        response.Raw.ShouldBe("not_json");
    }

    [Fact]
    public async Task Http_error_with_json_content_should_be_handled_correctly()
    {
        var content = new
        {
            foo = "foo",
            bar = "bar"
        };

        var handler = new NetworkHandler(JsonSerializer.Serialize(content), HttpStatusCode.Unauthorized);

        var client = new HttpClient(handler);
        var response = await client.RequestTokenAsync(new TokenRequest
        {
            Address = Endpoint,
            GrantType = "test",
            ClientId = "client"
        });

        response.IsError.ShouldBeTrue();
        response.ErrorType.ShouldBe(ResponseErrorType.Http);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Error.ShouldBe("Unauthorized");

        response.Json?.TryGetString("foo").ShouldBe("foo");
        response.Json?.TryGetString("bar").ShouldBe("bar");
    }
}
