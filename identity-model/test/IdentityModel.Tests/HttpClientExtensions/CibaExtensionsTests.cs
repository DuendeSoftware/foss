// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityModel.Infrastructure;

using Microsoft.AspNetCore.WebUtilities;

namespace Duende.IdentityModel.HttpClientExtensions;

public class CibaExtensionsTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private const string Endpoint = "http://server/backchannel";

    [Fact]
    public async Task Http_request_should_have_correct_format()
    {
        var handler = new NetworkHandler(HttpStatusCode.NotFound, "not found");

        var client = new HttpClient(handler);
        var request = new BackchannelAuthenticationRequest
        {
            Address = Endpoint,
            ClientId = "client",

            Scope = "scope",
            AcrValues = "acr_values",
            BindingMessage = "binding_message",
            ClientNotificationToken = "client_notification_token",
            UserCode = "user_code",

            RequestedExpiry = 1,

            IdTokenHint = "id_token_hint",
            LoginHintToken = "login_hint_token",
            LoginHint = "login_hint",

            Resource =
            {
                "resource1",
                "resource2"
            }
        };

        request.Headers.Add("custom", "custom");
        request.GetProperties().Add("custom", "custom");

        var response = await client.RequestBackchannelAuthenticationAsync(request, _ct);

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

        var fields = QueryHelpers.ParseQuery(handler.Body);
        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.Scope, out var scope).ShouldBeTrue();
        scope.First().ShouldBe("scope");

        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.AcrValues, out var acr_values).ShouldBeTrue();
        acr_values.First().ShouldBe("acr_values");

        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.BindingMessage, out var binding_message).ShouldBeTrue();
        binding_message.First().ShouldBe("binding_message");

        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.ClientNotificationToken, out var client_notification_token).ShouldBeTrue();
        client_notification_token.First().ShouldBe("client_notification_token");

        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.UserCode, out var user_code).ShouldBeTrue();
        user_code.First().ShouldBe("user_code");

        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.RequestedExpiry, out var request_expiry).ShouldBeTrue();
        int.Parse(request_expiry.First()).ShouldBe(1);

        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.IdTokenHint, out var id_token_hint).ShouldBeTrue();
        id_token_hint.First().ShouldBe("id_token_hint");

        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.LoginHintToken, out var login_hint_token).ShouldBeTrue();
        login_hint_token.First().ShouldBe("login_hint_token");

        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.LoginHint, out var login_hint).ShouldBeTrue();
        login_hint.First().ShouldBe("login_hint");

        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.Resource, out var resource).ShouldBeTrue();
        resource.Count.ShouldBe(2);
        resource.First().ShouldBe("resource1");
        resource.Skip(1).First().ShouldBe("resource2");
    }

    [Fact]
    public async Task Http_request_with_request_object_should_have_correct_format()
    {
        var handler = new NetworkHandler(HttpStatusCode.NotFound, "not found");

        var client = new HttpClient(handler);
        var request = new BackchannelAuthenticationRequest
        {
            Address = Endpoint,
            RequestObject = "request",

            ClientId = "client",

            Scope = "scope",
            AcrValues = "acr_values",
            BindingMessage = "binding_message",
            ClientNotificationToken = "client_notification_token",
            UserCode = "user_code",

            RequestedExpiry = 1,

            IdTokenHint = "id_token_hint",
            LoginHintToken = "login_hint_token",
            LoginHint = "login_hint",

            Resource =
            {
                "resource1",
                "resource2"
            }
        };

        request.Headers.Add("custom", "custom");
        request.GetProperties().Add("custom", "custom");

        var response = await client.RequestBackchannelAuthenticationAsync(request, _ct);

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

        var fields = QueryHelpers.ParseQuery(handler.Body);
        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.Scope, out var scope).ShouldBeFalse();
        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.AcrValues, out var _).ShouldBeFalse();
        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.BindingMessage, out _).ShouldBeFalse();
        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.ClientNotificationToken, out _).ShouldBeFalse();
        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.UserCode, out _).ShouldBeFalse();
        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.RequestedExpiry, out _).ShouldBeFalse();
        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.IdTokenHint, out _).ShouldBeFalse();
        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.LoginHintToken, out _).ShouldBeFalse();
        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.LoginHint, out _).ShouldBeFalse();
        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.Resource, out _).ShouldBeFalse();

        fields.TryGetValue(OidcConstants.BackchannelAuthenticationRequest.Request, out var ro).ShouldBeTrue();
        ro.First().ShouldBe("request");
    }

    [Fact]
    public async Task Valid_protocol_response_should_be_handled_correctly()
    {
        var document = File.ReadAllText(FileName.Create("success_ciba_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.OK);

        var client = new HttpClient(handler);
        var response = await client.RequestBackchannelAuthenticationAsync(new BackchannelAuthenticationRequest
        {
            Address = Endpoint,
            ClientId = "client",
            Scope = "scope"
        });

        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);

        response.AuthenticationRequestId.ShouldBe("1c266114-a1be-4252-8ad1-04986c5b9ac1");
        response.ExpiresIn.ShouldBe(120);
        response.Interval.ShouldBe(2);
    }

    //
    // [Fact]
    // public async Task Valid_protocol_error_should_be_handled_correctly()
    // {
    //     var document = File.ReadAllText(FileName.Create("failure_device_authorization_response.json"));
    //     var handler = new NetworkHandler(document, HttpStatusCode.BadRequest);
    //
    //     var client = new HttpClient(handler);
    //     var response = await client.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
    //     {
    //         Address = Endpoint,
    //         ClientId = "client"
    //     });
    //
    //     response.IsError.ShouldBeTrue();
    //     response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
    //     response.HttpStatusCode.ShouldBe(HttpStatusCode.BadRequest);
    //     response.Error.ShouldBe("error");
    //     response.ErrorDescription.ShouldBe("error_description");
    //     response.TryGet("custom").ShouldBe("custom");
    // }
    //
    // [Fact]
    // public async Task Malformed_response_document_should_be_handled_correctly()
    // {
    //     var document = "invalid";
    //     var handler = new NetworkHandler(document, HttpStatusCode.OK);
    //
    //     var client = new HttpClient(handler);
    //     var response = await client.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
    //     {
    //         Address = Endpoint,
    //         ClientId = "client"
    //     });
    //
    //     response.IsError.ShouldBeTrue();
    //     response.ErrorType.ShouldBe(ResponseErrorType.Exception);
    //     response.Raw.ShouldBe("invalid");
    //     response.Exception.ShouldNotBeNull();
    // }
    //
    // [Fact]
    // public async Task Exception_should_be_handled_correctly()
    // {
    //     var handler = new NetworkHandler(new Exception("exception"));
    //
    //     var client = new HttpClient(handler);
    //     var response = await client.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
    //     {
    //         Address = Endpoint,
    //         ClientId = "client"
    //     });
    //
    //     response.IsError.ShouldBeTrue();
    //     response.ErrorType.ShouldBe(ResponseErrorType.Exception);
    //     response.Error.ShouldBe("exception");
    //     response.Exception.ShouldNotBeNull();
    // }
    //
    // [Fact]
    // public async Task Http_error_should_be_handled_correctly()
    // {
    //     var handler = new NetworkHandler(HttpStatusCode.NotFound, "not found");
    //
    //     var client = new HttpClient(handler);
    //     var response = await client.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
    //     {
    //         Address = Endpoint,
    //         ClientId = "client"
    //     });
    //
    //     response.IsError.ShouldBeTrue();
    //     response.ErrorType.ShouldBe(ResponseErrorType.Http);
    //     response.HttpStatusCode.ShouldBe(HttpStatusCode.NotFound);
    //     response.Error.ShouldBe("not found");
    // }
    //
    // [Fact]
    // public async Task Http_error_with_non_json_content_should_be_handled_correctly()
    // {
    //     var handler = new NetworkHandler("not_json", HttpStatusCode.Unauthorized);
    //
    //     var client = new HttpClient(handler);
    //     var response = await client.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
    //     {
    //         Address = Endpoint,
    //         ClientId = "client"
    //     });
    //
    //     response.IsError.ShouldBeTrue();
    //     response.ErrorType.ShouldBe(ResponseErrorType.Http);
    //     response.HttpStatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    //     response.Error.ShouldBe("Unauthorized");
    //     response.Raw.ShouldBe("not_json");
    // }
    //
    // [Fact]
    // public async Task Http_error_with_json_content_should_be_handled_correctly()
    // {
    //     var content = new
    //     {
    //         foo = "foo",
    //         bar = "bar"
    //     };
    //
    //     var handler = new NetworkHandler(JsonSerializer.Serialize(content), HttpStatusCode.Unauthorized);
    //
    //     var client = new HttpClient(handler);
    //     var response = await client.RequestDeviceAuthorizationAsync(new DeviceAuthorizationRequest
    //     {
    //         Address = Endpoint,
    //         ClientId = "client"
    //     });
    //
    //     response.IsError.ShouldBeTrue();
    //     response.ErrorType.ShouldBe(ResponseErrorType.Http);
    //     response.HttpStatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    //     response.Error.ShouldBe("Unauthorized");
    //
    //     response.Json?.TryGetString("foo").ShouldBe("foo");
    //     response.Json?.TryGetString("bar").ShouldBe("bar");
    // }
}
