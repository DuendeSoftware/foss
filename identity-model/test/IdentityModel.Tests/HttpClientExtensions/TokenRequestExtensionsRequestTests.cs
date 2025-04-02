// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityModel.Infrastructure;

using Microsoft.AspNetCore.WebUtilities;

namespace Duende.IdentityModel.HttpClientExtensions;

public class TokenRequestExtensionsRequestTests
{
    private const string Endpoint = "http://server/token";

    private HttpClient _client;
    private NetworkHandler _handler;

    public TokenRequestExtensionsRequestTests()
    {
        var document = File.ReadAllText(FileName.Create("success_token_response.json"));
        _handler = new NetworkHandler(document, HttpStatusCode.OK);

        _client = new HttpClient(_handler)
        {
            BaseAddress = new Uri(Endpoint)
        };
    }

    [Fact]
    public async Task Http_request_should_have_correct_format()
    {
        var handler = new NetworkHandler(HttpStatusCode.NotFound, "not found");

        var client = new HttpClient(handler);
        var request = new TokenRequest
        {
            Address = Endpoint,
            ClientId = "client",

            GrantType = "grant"
        };

        request.Headers.Add("custom", "custom");
        request.GetProperties().Add("custom", "custom");

        var _ = await client.RequestTokenAsync(request);
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
    public async Task No_explicit_endpoint_address_should_use_base_address()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        { ClientId = "client" });

        response.IsError.ShouldBeFalse();
        _handler.Request.RequestUri.AbsoluteUri.ShouldBe(Endpoint);
    }

    [Fact]
    public async Task Repeating_a_request_should_succeed()
    {
        var request = new ClientCredentialsTokenRequest
        {
            ClientId = "client",
            Scope = "scope"
        };

        var response = await _client.RequestClientCredentialsTokenAsync(request);
        response.IsError.ShouldBeFalse();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("grant_type", out var grant_type).ShouldBeTrue();
        grant_type.First().ShouldBe(OidcConstants.GrantTypes.ClientCredentials);

        fields.TryGetValue("scope", out var scope).ShouldBeTrue();
        scope.First().ShouldBe("scope");

        response = await _client.RequestClientCredentialsTokenAsync(request);
        response.IsError.ShouldBeFalse();

        fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("grant_type", out grant_type).ShouldBeTrue();
        grant_type.First().ShouldBe(OidcConstants.GrantTypes.ClientCredentials);

        fields.TryGetValue("scope", out scope).ShouldBeTrue();
        scope.First().ShouldBe("scope");
    }

    [Fact]
    public async Task Client_credentials_request_should_have_correct_format()
    {
        var response = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            ClientId = "client",
            Scope = "scope",
            Resource = { "resource1", "resource2" }
        });

        response.IsError.ShouldBeFalse();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("grant_type", out var grant_type).ShouldBeTrue();
        grant_type.First().ShouldBe(OidcConstants.GrantTypes.ClientCredentials);

        fields.TryGetValue("scope", out var scope).ShouldBeTrue();
        scope.First().ShouldBe("scope");

        fields.TryGetValue("resource", out var resource).ShouldBeTrue();
        resource.Count.ShouldBe(2);
        resource[0].ShouldBe("resource1");
        resource[1].ShouldBe("resource2");
    }

    [Fact]
    public async Task Additional_headers_should_be_propagated()
    {
        var request = new ClientCredentialsTokenRequest
        {
            ClientId = "client",
            Scope = "scope"
        };

        request.Headers.Add("foo", "bar");

        var response = await _client.RequestClientCredentialsTokenAsync(request);

        response.IsError.ShouldBeFalse();

        var headers = _handler.Request.Headers;
        var foo = headers.FirstOrDefault(h => h.Key == "foo");
        foo.Value.Single().ShouldBe("bar");
    }

    [Fact]
    public async Task Additional_request_properties_should_be_propagated()
    {
        var request = new ClientCredentialsTokenRequest
        {
            ClientId = "client",
            Scope = "scope"
        };

        request.GetProperties().Add("foo", "bar");

        var response = await _client.RequestClientCredentialsTokenAsync(request);

        response.IsError.ShouldBeFalse();

        var properties = _handler.Request.GetProperties();
        var foo = properties.First().Value as string;
        foo.ShouldNotBeNull();
        foo.ShouldBe("bar");
    }

    [Fact]
    public async Task dpop_proof_token_should_be_propagated()
    {
        var request = new ClientCredentialsTokenRequest
        {
            ClientId = "client",
            Scope = "scope",
            DPoPProofToken = "dpop_token"
        };

        var response = await _client.RequestClientCredentialsTokenAsync(request);

        response.IsError.ShouldBeFalse();
        _handler.Request.Headers.Single(x => x.Key == "DPoP").Value.First().ShouldBe("dpop_token");
    }

    [Fact]
    public async Task dpop_nonce_should_be_returned()
    {
        _handler = new NetworkHandler(req =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.BadRequest);
            resp.Headers.Add("DPoP-Nonce", "dpop_nonce");
            resp.Content = new FormUrlEncodedContent(new Dictionary<string, string>());
            return resp;
        });
        _client = new HttpClient(_handler)
        {
            BaseAddress = new Uri(Endpoint)
        };

        var request = new ClientCredentialsTokenRequest
        {
            ClientId = "client",
            Scope = "scope",
        };

        var response = await _client.RequestClientCredentialsTokenAsync(request);

        response.IsError.ShouldBeTrue();
        response.DPoPNonce.ShouldBe("dpop_nonce");
    }


    [Fact]
    public async Task Explicit_null_parameters_should_not_fail_()
    {
        Func<Task> act = async () =>
            await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            { ClientId = "client", Parameters = null });

        await act.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task Device_request_should_have_correct_format()
    {
        var response = await _client.RequestDeviceTokenAsync(new DeviceTokenRequest
        {
            ClientId = "device",
            DeviceCode = "device_code"
        });

        response.IsError.ShouldBeFalse();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("grant_type", out var grant_type).ShouldBeTrue();
        grant_type.First().ShouldBe(OidcConstants.GrantTypes.DeviceCode);

        fields.TryGetValue("device_code", out var device_code).ShouldBeTrue();
        device_code.First().ShouldBe("device_code");
    }

    [Fact]
    public async Task Device_request_without_device_code_should_fail()
    {
        Func<Task> act = async () =>
            await _client.RequestDeviceTokenAsync(new DeviceTokenRequest { ClientId = "device" });

        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.ParamName.ShouldBe("device_code");
    }

    [Fact]
    public async Task Password_request_should_have_correct_format()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            ClientId = "client",
            UserName = "user",
            Password = "password",
            Scope = "scope",
            Resource = { "resource1", "resource2" }
        });

        response.IsError.ShouldBeFalse();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("grant_type", out var grant_type).ShouldBeTrue();
        grant_type.First().ShouldBe(OidcConstants.GrantTypes.Password);

        fields.TryGetValue("username", out var username).ShouldBeTrue();
        username.First().ShouldBe("user");

        fields.TryGetValue("password", out var password).ShouldBeTrue();
        grant_type.First().ShouldBe("password");

        fields.TryGetValue("scope", out var scope).ShouldBeTrue();
        scope.First().ShouldBe("scope");

        fields.TryGetValue("resource", out var resource).ShouldBeTrue();
        resource.Count.ShouldBe(2);
        resource[0].ShouldBe("resource1");
        resource[1].ShouldBe("resource2");
    }

    [Fact]
    public async Task Password_request_without_password_should_have_correct_format()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            ClientId = "client",
            UserName = "user"
        });

        response.IsError.ShouldBeFalse();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("grant_type", out var grant_type).ShouldBeTrue();
        grant_type.First().ShouldBe(OidcConstants.GrantTypes.Password);

        fields.TryGetValue("username", out var username).ShouldBeTrue();
        username.First().ShouldBe("user");

        fields.TryGetValue("password", out var password).ShouldBeTrue();
        password.First().ShouldBe("");
    }

    [Fact]
    public async Task Password_request_without_username_should_fail()
    {
        Func<Task> act = async () => await _client.RequestPasswordTokenAsync(new PasswordTokenRequest());

        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.ParamName.ShouldBe("username");
    }

    [Fact]
    public async Task Code_request_should_have_correct_format()
    {
        var response = await _client.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            ClientId = "client",
            Code = "code",
            RedirectUri = "uri",
            CodeVerifier = "verifier",
            Resource = { "resource1", "resource2" },
        });

        response.IsError.ShouldBeFalse();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("grant_type", out var grant_type).ShouldBeTrue();
        grant_type.First().ShouldBe(OidcConstants.GrantTypes.AuthorizationCode);

        fields.TryGetValue("code", out var code).ShouldBeTrue();
        code.First().ShouldBe("code");

        fields.TryGetValue("redirect_uri", out var redirect_uri).ShouldBeTrue();
        redirect_uri.First().ShouldBe("uri");

        fields.TryGetValue("code_verifier", out var code_verifier).ShouldBeTrue();
        code_verifier.First().ShouldBe("verifier");

        fields.TryGetValue("resource", out var resource).ShouldBeTrue();
        resource.Count.ShouldBe(2);
        resource[0].ShouldBe("resource1");
        resource[1].ShouldBe("resource2");
    }

    [Fact]
    public async Task Code_request_without_code_should_fail()
    {
        Func<Task> act = async () => await _client.RequestAuthorizationCodeTokenAsync(
            new AuthorizationCodeTokenRequest
            {
                RedirectUri = "uri"
            });

        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.ParamName.ShouldBe("code");
    }

    [Fact]
    public async Task Code_request_without_redirect_uri_should_fail()
    {
        Func<Task> act = async () => await _client.RequestAuthorizationCodeTokenAsync(
            new AuthorizationCodeTokenRequest
            {
                Code = "code"
            });

        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.ParamName.ShouldBe("redirect_uri");
    }

    [Fact]
    public async Task Refresh_request_should_have_correct_format()
    {
        var response = await _client.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            ClientId = "client",
            RefreshToken = "rt",
            Scope = "scope",
            Resource = { "resource1", "resource2" }
        });

        response.IsError.ShouldBeFalse();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("grant_type", out var grant_type).ShouldBeTrue();
        grant_type.First().ShouldBe(OidcConstants.GrantTypes.RefreshToken);

        fields.TryGetValue("refresh_token", out var code).ShouldBeTrue();
        code.First().ShouldBe("rt");

        fields.TryGetValue("scope", out var redirect_uri).ShouldBeTrue();
        redirect_uri.First().ShouldBe("scope");

        fields.TryGetValue("resource", out var resource).ShouldBeTrue();
        resource.Count.ShouldBe(2);
        resource[0].ShouldBe("resource1");
        resource[1].ShouldBe("resource2");
    }

    [Fact]
    public async Task Refresh_request_without_refresh_token_should_fail()
    {
        Func<Task> act = async () => await _client.RequestRefreshTokenAsync(new RefreshTokenRequest());

        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.ParamName.ShouldBe("refresh_token");
    }

    [Fact]
    public async Task TokenExchange_request_should_have_correct_format()
    {
        var response = await _client.RequestTokenExchangeTokenAsync(new TokenExchangeTokenRequest
        {
            ClientId = "client",

            SubjectToken = "subject_token",
            SubjectTokenType = "subject_token_type",

            Scope = "scope",
            Resource = "resource",
            Audience = "audience",

            ActorToken = "actor_token",
            ActorTokenType = "actor_token_type"
        });

        response.IsError.ShouldBeFalse();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("grant_type", out var grant_type).ShouldBeTrue();
        grant_type.First().ShouldBe(OidcConstants.GrantTypes.TokenExchange);

        fields.TryGetValue("subject_token", out var subject_token).ShouldBeTrue();
        subject_token.First().ShouldBe("subject_token");

        fields.TryGetValue("subject_token_type", out var subject_token_type).ShouldBeTrue();
        subject_token_type.First().ShouldBe("subject_token_type");

        fields.TryGetValue("scope", out var scope).ShouldBeTrue();
        scope.First().ShouldBe("scope");

        fields.TryGetValue("resource", out var resource).ShouldBeTrue();
        resource.First().ShouldBe("resource");

        fields.TryGetValue("audience", out var audience).ShouldBeTrue();
        audience.First().ShouldBe("audience");

        fields.TryGetValue("actor_token", out var actor_token).ShouldBeTrue();
        actor_token.First().ShouldBe("actor_token");

        fields.TryGetValue("actor_token_type", out var actor_token_type).ShouldBeTrue();
        actor_token_type.First().ShouldBe("actor_token_type");
    }

    [Fact]
    public async Task Backchannel_authentication_request_should_have_correct_format()
    {
        var response = await _client.RequestBackchannelAuthenticationTokenAsync(new BackchannelAuthenticationTokenRequest()
        {
            ClientId = "client",
            AuthenticationRequestId = "id",

            Resource =
            {
                "resource1",
                "resource2"
            }
        });

        response.IsError.ShouldBeFalse();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("grant_type", out var grant_type).ShouldBeTrue();
        grant_type.First().ShouldBe(OidcConstants.GrantTypes.Ciba);

        fields.TryGetValue("auth_req_id", out var id).ShouldBeTrue();
        id.First().ShouldBe("id");

        fields.TryGetValue(OidcConstants.TokenRequest.Resource, out var resource).ShouldBeTrue();
        resource.Count.ShouldBe(2);
        resource.First().ShouldBe("resource1");
        resource.Skip(1).First().ShouldBe("resource2");
    }

    [Fact]
    public async Task Setting_no_grant_type_should_fail()
    {
        Func<Task> act = async () => await _client.RequestTokenAsync(new TokenRequest());

        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.ParamName.ShouldBe("grant_type");
    }

    [Fact]
    public async Task Setting_custom_parameters_should_have_correct_format()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            GrantType = "test",
            Parameters =
            {
                { "client_id", "custom" },
                { "client_secret", "custom" },
                { "custom", "custom" }
            }
        });

        var request = _handler.Request;

        request.Headers.Authorization.ShouldBeNull();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("grant_type", out var grant_type).ShouldBeTrue();
        grant_type.First().ShouldBe("test");

        fields.TryGetValue("client_id", out var client_id).ShouldBeTrue();
        client_id.First().ShouldBe("custom");

        fields.TryGetValue("client_secret", out var client_secret).ShouldBeTrue();
        client_secret.First().ShouldBe("custom");

        fields.TryGetValue("custom", out var custom).ShouldBeTrue();
        custom.First().ShouldBe("custom");
    }

    [Fact]
    public async Task Setting_grant_type_via_optional_parameters_should_create_correct_format()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            ClientId = "client",
            GrantType = "test",
            Parameters =
            {
                { "grant_type", "custom" },
                { "custom", "custom" }
            }
        });

        var request = _handler.Request;

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("grant_type", out var grant_type).ShouldBeTrue();
        grant_type.First().ShouldBe("custom");

        fields.TryGetValue("custom", out var custom).ShouldBeTrue();
        custom.First().ShouldBe("custom");
    }

    [Fact]
    public async Task Sending_raw_parameters_should_create_correct_format()
    {
        var response = await _client.RequestTokenRawAsync("https://token/", new Parameters
        {
            { "grant_type", "test" },
            { "client_id", "client" },
            { "client_secret", "secret" },
            { "scope", "scope" }
        });

        var request = _handler.Request;

        request.RequestUri.AbsoluteUri.ShouldBe("https://token/");


        var fields = QueryHelpers.ParseQuery(_handler.Body);

        fields.TryGetValue("grant_type", out var field).ShouldBeTrue();
        field.First().ShouldBe("test");

        fields.TryGetValue("client_id", out field).ShouldBeTrue();
        field.First().ShouldBe("client");

        fields.TryGetValue("client_secret", out field).ShouldBeTrue();
        field.First().ShouldBe("secret");

        fields.TryGetValue("scope", out field).ShouldBeTrue();
        field.First().ShouldBe("scope");
    }

    [Fact]
    public async Task Setting_basic_authentication_style_should_send_basic_authentication_header()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            GrantType = "test",
            ClientId = "client",
            ClientSecret = "secret",
            ClientCredentialStyle = ClientCredentialStyle.AuthorizationHeader
        });

        var request = _handler.Request;

        request.Headers.Authorization.ShouldNotBeNull();
        request.Headers.Authorization.Scheme.ShouldBe("Basic");
        request.Headers.Authorization.Parameter
            .ShouldBe(BasicAuthenticationOAuthHeaderValue.EncodeCredential("client", "secret"));
    }

    [Fact]
    public async Task Setting_post_values_authentication_style_should_post_values()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            GrantType = "test",
            ClientId = "client",
            ClientSecret = "secret",
            ClientCredentialStyle = ClientCredentialStyle.PostBody
        });

        var request = _handler.Request;
        request.Headers.Authorization.ShouldBeNull();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields["client_id"].First().ShouldBe("client");
        fields["client_secret"].First().ShouldBe("secret");
    }

    [Fact]
    public async Task Setting_client_id_only_and_post_should_put_client_id_in_post_body()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            GrantType = "test",
            ClientId = "client",
            ClientCredentialStyle = ClientCredentialStyle.PostBody
        });

        var request = _handler.Request;

        request.Headers.Authorization.ShouldBeNull();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields["client_id"].First().ShouldBe("client");
    }

    [Fact]
    public async Task Setting_client_id_only_and_header_should_put_client_id_in_header()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            GrantType = "test",
            ClientId = "client",
            ClientCredentialStyle = ClientCredentialStyle.AuthorizationHeader
        });

        var request = _handler.Request;

        request.Headers.Authorization.ShouldNotBeNull();
        request.Headers.Authorization.Scheme.ShouldBe("Basic");
        request.Headers.Authorization.Parameter
            .ShouldBe(BasicAuthenticationOAuthHeaderValue.EncodeCredential("client", ""));

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("client_secret", out _).ShouldBeFalse();
        fields.TryGetValue("client_id", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Setting_client_id_and_assertion_with_authorization_header_should_fail()
    {
        Func<Task> act = () => _client.RequestTokenAsync(new TokenRequest
        {
            GrantType = "test",
            ClientId = "client",
            ClientAssertion = { Type = "type", Value = "value" },
            ClientCredentialStyle = ClientCredentialStyle.AuthorizationHeader
        });

        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldBe("CredentialStyle.AuthorizationHeader and client assertions are not compatible");
    }

    [Fact]
    public async Task Setting_client_id_and_assertion_should_have_correct_format()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            GrantType = "test",
            ClientId = "client",
            ClientAssertion = { Type = "type", Value = "value" },
            ClientCredentialStyle = ClientCredentialStyle.PostBody
        });

        var request = _handler.Request;

        var fields = QueryHelpers.ParseQuery(_handler.Body);

        fields["grant_type"].First().ShouldBe("test");
        fields["client_id"].First().ShouldBe("client");
        fields["client_assertion_type"].First().ShouldBe("type");
        fields["client_assertion"].First().ShouldBe("value");
    }

    [Fact]
    public async Task Setting_assertion_without_client_id_and_authz_header_should_have_correct_format()
    {
        var response = await _client.RequestTokenAsync(new TokenRequest
        {
            GrantType = "test",
            ClientAssertion = { Type = "type", Value = "value" },
            ClientCredentialStyle = ClientCredentialStyle.AuthorizationHeader
        });

        var request = _handler.Request;

        var fields = QueryHelpers.ParseQuery(_handler.Body);

        fields["grant_type"].First().ShouldBe("test");
        fields["client_assertion_type"].First().ShouldBe("type");
        fields["client_assertion"].First().ShouldBe("value");
    }
}
