// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.Infrastructure;

using Microsoft.AspNetCore.WebUtilities;

namespace Duende.IdentityModel.HttpClientExtensions;

public class DeviceAuthorizationExtensionsTests
{
    private const string Endpoint = "http://server/device";

    [Fact]
    public async Task Request_without_body_should_have_correct_format()
    {
        var handler = new NetworkHandler(HttpStatusCode.NotFound, "not found");

        var client = new HttpClient(handler);
        var request = new DeviceAuthorizationRequest
        {
            Address = Endpoint,
            ClientId = "client",

            //ClientCredentialStyle = ClientCredentialStyle.PostBody
        };

        var _ = await client.RequestDeviceAuthorizationAsync(request);

        var httpRequest = handler.Request;

        httpRequest.Method.ShouldBe(HttpMethod.Post);
        httpRequest.RequestUri.ShouldBe(new Uri(Endpoint));
        httpRequest.Content.ShouldBeOfType<FormUrlEncodedContent>();

        var headers = httpRequest.Headers;
        headers.Count().ShouldBe(2);
    }

    [Fact]
    public async Task Http_request_should_have_correct_format()
    {
        var handler = new NetworkHandler(HttpStatusCode.NotFound, "not found");

        var client = new HttpClient(handler);
        var request = new DeviceAuthorizationRequest
        {
            Address = Endpoint,
            ClientId = "client"
        };

        request.Headers.Add("custom", "custom");
        request.GetProperties().Add("custom", "custom");

        var _ = await client.RequestDeviceAuthorizationAsync(request);

        var httpRequest = handler.Request;

        httpRequest.Method.ShouldBe(HttpMethod.Post);
        httpRequest.RequestUri.ShouldBe(new Uri(Endpoint));

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
    public async Task Setting_basic_authentication_style_should_send_basic_authentication_header()
    {
        var document = File.ReadAllText(FileName.Create("success_device_authorization_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler);
        var response = await client.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
        {
            Address = Endpoint,
            ClientId = "client",
            ClientSecret = "secret",
            ClientCredentialStyle = ClientCredentialStyle.AuthorizationHeader
        });

        var request = handler.Request;

        request.Headers.Authorization.ShouldNotBeNull();
        request.Headers.Authorization.Scheme.ShouldBe("Basic");
        request.Headers.Authorization.Parameter.ShouldBe(BasicAuthenticationOAuthHeaderValue.EncodeCredential("client", "secret"));
    }

    [Fact]
    public async Task Setting_post_values_authentication_style_should_post_values()
    {
        var document = File.ReadAllText(FileName.Create("success_device_authorization_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler);
        var response = await client.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
        {
            Address = Endpoint,
            ClientId = "client",
            ClientSecret = "secret",
            ClientCredentialStyle = ClientCredentialStyle.PostBody
        });

        var request = handler.Request;

        request.Headers.Authorization.ShouldBeNull();

        var fields = QueryHelpers.ParseQuery(handler.Body);
        fields["client_id"].First().ShouldBe("client");
        fields["client_secret"].First().ShouldBe("secret");
    }

    [Fact]
    public async Task Valid_protocol_response_should_be_handled_correctly()
    {
        var document = File.ReadAllText(FileName.Create("success_device_authorization_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler);
        var response = await client.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
        {
            Address = Endpoint,
            ClientId = "client"
        });

        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);

        response.DeviceCode.ShouldBe("GMMhmHCXhWEzkobqIHGG_EnNYYsAkukHspeYUk9E8");
        response.UserCode.ShouldBe("WDJB-MJHT");
        response.VerificationUri.ShouldBe("https://www.example.com/device");
        response.VerificationUriComplete.ShouldBe("https://www.example.com/device?user_code=WDJB-MJHT");

        response.ExpiresIn.ShouldBe(1800);
        response.Interval.ShouldBe(10);
    }

    [Fact]
    public async Task Repeating_a_request_should_succeed()
    {
        var document = File.ReadAllText(FileName.Create("success_device_authorization_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var request = new DeviceAuthorizationRequest
        {
            Address = Endpoint,
            ClientId = "client"
        };

        var client = new HttpClient(handler);
        var response = await client.RequestDeviceAuthorizationAsync(request);

        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);

        response.DeviceCode.ShouldBe("GMMhmHCXhWEzkobqIHGG_EnNYYsAkukHspeYUk9E8");
        response.UserCode.ShouldBe("WDJB-MJHT");
        response.VerificationUri.ShouldBe("https://www.example.com/device");
        response.VerificationUriComplete.ShouldBe("https://www.example.com/device?user_code=WDJB-MJHT");

        response.ExpiresIn.ShouldBe(1800);
        response.Interval.ShouldBe(10);

        // repeat
        response = await client.RequestDeviceAuthorizationAsync(request);

        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);

        response.DeviceCode.ShouldBe("GMMhmHCXhWEzkobqIHGG_EnNYYsAkukHspeYUk9E8");
        response.UserCode.ShouldBe("WDJB-MJHT");
        response.VerificationUri.ShouldBe("https://www.example.com/device");
        response.VerificationUriComplete.ShouldBe("https://www.example.com/device?user_code=WDJB-MJHT");

        response.ExpiresIn.ShouldBe(1800);
        response.Interval.ShouldBe(10);
    }

    [Fact]
    public async Task Valid_protocol_error_should_be_handled_correctly()
    {
        var document = File.ReadAllText(FileName.Create("failure_device_authorization_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.BadRequest);

        var client = new HttpClient(handler);
        var response = await client.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
        {
            Address = Endpoint,
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
        var response = await client.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
        {
            Address = Endpoint,
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
        var response = await client.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
        {
            Address = Endpoint,
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
        var response = await client.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
        {
            Address = Endpoint,
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
        var response = await client.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
        {
            Address = Endpoint,
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
        var response = await client.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
        {
            Address = Endpoint,
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
