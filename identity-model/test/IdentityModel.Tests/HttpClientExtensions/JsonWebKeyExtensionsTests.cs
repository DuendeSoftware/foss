// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.Infrastructure;


namespace Duende.IdentityModel.HttpClientExtensions;

public class JsonWebkeyExtensionsTests
{
    private readonly NetworkHandler _successHandler;
    private readonly string _endpoint = "https://demo.identityserver.io/.well-known/openid-configuration/jwks";

    public JsonWebkeyExtensionsTests()
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
        var request = new JsonWebKeySetRequest
        {
            Address = _endpoint
        };

        request.Headers.Add("custom", "custom");
        request.GetProperties().Add("custom", "custom");

        var response = await client.GetJsonWebKeySetAsync(request);

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

        var jwk = await client.GetJsonWebKeySetAsync();

        jwk.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task Explicit_address_should_work()
    {
        var client = new HttpClient(_successHandler);

        var jwk = await client.GetJsonWebKeySetAsync(_endpoint);

        jwk.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task Http_error_should_be_handled_correctly()
    {
        var handler = new NetworkHandler(HttpStatusCode.NotFound, "not found");
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(_endpoint)
        };

        var jwk = await client.GetJsonWebKeySetAsync();

        jwk.IsError.ShouldBeTrue();
        jwk.ErrorType.ShouldBe(ResponseErrorType.Http);
        jwk.Error.ShouldStartWith("Error connecting to");
        jwk.Error.ShouldEndWith("not found");
        jwk.HttpStatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Exception_should_be_handled_correctly()
    {
        var handler = new NetworkHandler(new Exception("error"));

        var client = new HttpClient(handler);
        var jwk = await client.GetJsonWebKeySetAsync(_endpoint);

        jwk.IsError.ShouldBeTrue();
        jwk.ErrorType.ShouldBe(ResponseErrorType.Exception);
        jwk.Error.ShouldStartWith("Error connecting to");
        jwk.Error.ShouldEndWith("error.");
    }

    [Fact]
    public async Task Strongly_typed_accessors_should_behave_as_expected()
    {
        var client = new HttpClient(_successHandler)
        {
            BaseAddress = new Uri(_endpoint)
        };

        var jwk = await client.GetJsonWebKeySetAsync();

        jwk.IsError.ShouldBeFalse();
        jwk.KeySet.ShouldNotBeNull();
    }

    [Fact]
    public async Task Http_error_with_non_json_content_should_be_handled_correctly()
    {
        var handler = new NetworkHandler("not_json", HttpStatusCode.InternalServerError);
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(_endpoint)
        };

        var jwk = await client.GetJsonWebKeySetAsync();

        jwk.IsError.ShouldBeTrue();
        jwk.ErrorType.ShouldBe(ResponseErrorType.Http);
        jwk.HttpStatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        jwk.Error.ShouldContain("Internal Server Error");
        jwk.Raw.ShouldBe("not_json");
        jwk.Json?.ValueKind.ShouldBe(JsonValueKind.Undefined);
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

        var jwk = await client.GetJsonWebKeySetAsync();

        jwk.IsError.ShouldBeTrue();
        jwk.ErrorType.ShouldBe(ResponseErrorType.Http);
        jwk.HttpStatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        jwk.Error.ShouldContain("Internal Server Error");

        jwk.Json?.TryGetString("foo").ShouldBe("foo");
        jwk.Json?.TryGetString("bar").ShouldBe("bar");
    }
}
