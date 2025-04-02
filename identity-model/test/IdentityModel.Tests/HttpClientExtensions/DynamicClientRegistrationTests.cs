// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.Infrastructure;


namespace Duende.IdentityModel.HttpClientExtensions;

public class DynamicClientRegistrationTests
{
    private const string Endpoint = "http://server/register";

    // This is an example software statement, taken from RFC 7591
    private const string SoftwareStatement = "eyJhbGciOiJSUzI1NiJ9.eyJzb2Z0d2FyZV9pZCI6IjROUkIxLTBYWkFCWkk5RTYtNVNNM1IiLCJjbGllbnRfbmFtZSI6IkV4YW1wbGUgU3RhdGVtZW50LWJhc2VkIENsaWVudCIsImNsaWVudF91cmkiOiJodHRwczovL2NsaWVudC5leGFtcGxlLm5ldC8ifQ.GHfL4QNIrQwL18BSRdE595T9jbzqa06R9BT8w409x9oIcKaZo_mt15riEXHa  zdISUvDIZhtiyNrSHQ8K4TvqWxH6uJgcmoodZdPwmWRIEYbQDLqPNxREtYn05X3AR7ia4FRjQ2ojZjk5fJqJdQ-JcfxyhK-P8BAWBd6I2LLA77IG32xtbhxYfHX7VhuU5ProJO8uvu3Ayv4XRhLZJY4yKfmyjiiKiPNe-Ia4SMy_d_QSWxskU5XIQl5Sa2YRPMbDRXttm2TfnZM1xx70DoYi8g6czz-CPGRi4SW_S2RKHIJfIjoI3zTJ0Y2oe0_EJAiXbL6OyF9S5tKxDXV8JIndSA";

    [Fact]
    public async Task Http_request_should_have_correct_format()
    {
        var handler = new NetworkHandler(HttpStatusCode.NotFound, "not found");

        var client = new HttpClient(handler);
        var request = new DynamicClientRegistrationRequest
        {
            Address = Endpoint,
            Document = new DynamicClientRegistrationDocument()
        };

        request.Headers.Add("custom", "custom");
        request.GetProperties().Add("custom", "custom");

        var response = await client.RegisterClientAsync(request);

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
        var document = File.ReadAllText(FileName.Create("success_registration_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.Created);

        var client = new HttpClient(handler);
        var response = await client.RegisterClientAsync(new DynamicClientRegistrationRequest
        {
            Address = Endpoint,
            Document = new DynamicClientRegistrationDocument()
            {
                SoftwareStatement = SoftwareStatement
            }
        });

        response.IsError.ShouldBeFalse();
        response.ErrorType.ShouldBe(ResponseErrorType.None);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.Created);

        response.ClientId.ShouldBe("s6BhdRkqt3");
        response.ClientSecret.ShouldBe("ZJYCqe3GGRvdrudKyZS0XhGv_Z45DuKhCUk0gBR1vZk");
        response.ClientSecretExpiresAt.ShouldBe(1577858400);
        response.ClientIdIssuedAt.HasValue.ShouldBeFalse();
        response.RegistrationAccessToken.ShouldBe("this.is.an.access.token.value.ffx83");
        response.RegistrationClientUri.ShouldBe("https://server.example.com/connect/register?client_id=s6BhdRkqt3");
        // Spec requires that a software statement be echoed back unchanged
        response.SoftwareStatement.ShouldBe(SoftwareStatement);

        response.Json?.TryGetString(OidcConstants.ClientMetadata.TokenEndpointAuthenticationMethod)
            .ShouldBe(OidcConstants.EndpointAuthenticationMethods.BasicAuthentication);
    }

    [Fact]
    public async Task Malformed_response_document_should_be_handled_correctly()
    {
        var document = "invalid";
        var handler = new NetworkHandler(document, HttpStatusCode.Created);

        var client = new HttpClient(handler);
        var response = await client.RegisterClientAsync(new DynamicClientRegistrationRequest
        {
            Address = Endpoint,
            Document = new DynamicClientRegistrationDocument()
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
        var response = await client.RegisterClientAsync(new DynamicClientRegistrationRequest
        {
            Address = Endpoint,
            Document = new DynamicClientRegistrationDocument()
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
        var response = await client.RegisterClientAsync(new DynamicClientRegistrationRequest
        {
            Address = Endpoint,
            Document = new DynamicClientRegistrationDocument()
        });

        response.IsError.ShouldBeTrue();
        response.ErrorType.ShouldBe(ResponseErrorType.Http);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Error.ShouldBe("not found");
    }

    [Fact]
    public async Task Valid_protocol_error_should_be_handled_correctly()
    {
        var document = File.ReadAllText(FileName.Create("failure_registration_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.BadRequest);

        var client = new HttpClient(handler);
        var response = await client.RegisterClientAsync(new DynamicClientRegistrationRequest
        {
            Address = Endpoint,
            Document = new DynamicClientRegistrationDocument()
        });

        response.IsError.ShouldBeTrue();
        response.ErrorType.ShouldBe(ResponseErrorType.Protocol);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Error.ShouldBe("invalid_redirect_uri");
        response.ErrorDescription.ShouldBe("One or more redirect_uri values are invalid");
        response.TryGet("custom").ShouldBe("custom");
    }

    [Fact]
    public async Task Extensions_should_be_serializable()
    {
        var request = new DynamicClientRegistrationRequest
        {
            Address = Endpoint,
            Document = JsonSerializer.Deserialize<DynamicClientRegistrationDocument>(
                "{\"extension\":\"data\"}")
        };
        request.Document.Extensions.ShouldNotBeEmpty();

        var document = File.ReadAllText(FileName.Create("success_registration_response.json"));
        var handler = new NetworkHandler(document, HttpStatusCode.Created);

        var client = new HttpClient(handler);
        var response = await client.RegisterClientAsync(request);

        // Mostly we just want to make sure that serialization didn't throw
        response.ShouldNotBeNull();
    }
}
