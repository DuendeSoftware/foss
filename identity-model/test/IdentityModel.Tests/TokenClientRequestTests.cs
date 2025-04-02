// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityModel.Infrastructure;

using Microsoft.AspNetCore.WebUtilities;

namespace Duende.IdentityModel;

public class TokenClientRequestTests
{
    private const string Endpoint = "http://server/token";

    private readonly HttpClient _client;
    private readonly NetworkHandler _handler;

    public TokenClientRequestTests()
    {
        var document = File.ReadAllText(FileName.Create("success_token_response.json"));
        _handler = new NetworkHandler(document, HttpStatusCode.OK);

        _client = new HttpClient(_handler)
        {
            BaseAddress = new Uri(Endpoint)
        };
    }

    [Fact]
    public async Task No_explicit_endpoint_address_should_use_base_address()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions { ClientId = "client" });

        var response = await tokenClient.RequestClientCredentialsTokenAsync();

        response.IsError.ShouldBeFalse();
        _handler.Request.RequestUri.AbsoluteUri.ShouldBe(Endpoint);
    }

    [Fact]
    public async Task Client_credentials_request_should_have_correct_format()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions { ClientId = "client" });

        var response = await tokenClient.RequestClientCredentialsTokenAsync(scope: "scope");

        response.IsError.ShouldBeFalse();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("grant_type", out var grant_type).ShouldBeTrue();
        grant_type.First().ShouldBe(OidcConstants.GrantTypes.ClientCredentials);

        fields.TryGetValue("scope", out var scope).ShouldBeTrue();
        scope.First().ShouldBe("scope");
    }

    [Fact]
    public async Task Device_request_should_have_correct_format()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions { ClientId = "device" });

        var response = await tokenClient.RequestDeviceTokenAsync(deviceCode: "device_code");

        response.IsError.ShouldBeFalse();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("grant_type", out var grant_type).ShouldBeTrue();
        grant_type.First().ShouldBe(OidcConstants.GrantTypes.DeviceCode);

        fields.TryGetValue("client_id", out var client_id).ShouldBeTrue();
        client_id.First().ShouldBe("device");

        fields.TryGetValue("device_code", out var device_code).ShouldBeTrue();
        device_code.First().ShouldBe("device_code");
    }

    [Fact]
    public async Task Device_request_without_device_code_should_fail()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions { ClientId = "device" });

        Func<Task> act = async () => await tokenClient.RequestDeviceTokenAsync(null);

        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.ParamName.ShouldBe("device_code");
    }

    [Fact]
    public async Task Password_request_should_have_correct_format()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions { ClientId = "client" });

        var response = await tokenClient.RequestPasswordTokenAsync(userName: "user", password: "password", scope: "scope");

        response.IsError.ShouldBeFalse();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("grant_type", out var grant_type).ShouldBeTrue();
        grant_type.First().ShouldBe(OidcConstants.GrantTypes.Password);

        fields.TryGetValue("username", out var username).ShouldBeTrue();
        username.First().ShouldBe("user");

        fields.TryGetValue("password", out var password).ShouldBeTrue();
        password.First().ShouldBe("password");

        fields.TryGetValue("scope", out var scope).ShouldBeTrue();
        scope.First().ShouldBe("scope");
    }

    [Fact]
    public async Task Password_request_without_password_should_have_correct_format()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions { ClientId = "client" });

        var response = await tokenClient.RequestPasswordTokenAsync(userName: "user");

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
        var tokenClient = new TokenClient(_client, new TokenClientOptions());

        Func<Task> act = async () => await tokenClient.RequestPasswordTokenAsync(userName: null);

        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.ParamName.ShouldBe("username");
    }

    [Fact]
    public async Task Code_request_should_have_correct_format()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions { ClientId = "client" });

        var response = await tokenClient.RequestAuthorizationCodeTokenAsync(code: "code", redirectUri: "uri", codeVerifier: "verifier");

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
    }

    [Fact]
    public async Task Code_request_without_code_should_fail()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions());

        Func<Task> act = async () => await tokenClient.RequestAuthorizationCodeTokenAsync(code: null, redirectUri: "uri", codeVerifier: "verifier");

        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.ParamName.ShouldBe("code");
    }

    [Fact]
    public async Task Code_request_without_redirect_uri_should_fail()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions());

        Func<Task> act = async () => await tokenClient.RequestAuthorizationCodeTokenAsync(code: "code", redirectUri: null, codeVerifier: "verifier");

        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.ParamName.ShouldBe("redirect_uri");
    }

    [Fact]
    public async Task Refresh_request_should_have_correct_format()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions { ClientId = "client" });

        var response = await tokenClient.RequestRefreshTokenAsync(refreshToken: "rt", scope: "scope");

        response.IsError.ShouldBeFalse();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("grant_type", out var grant_type).ShouldBeTrue();
        grant_type.First().ShouldBe(OidcConstants.GrantTypes.RefreshToken);

        fields.TryGetValue("refresh_token", out var code).ShouldBeTrue();
        code.First().ShouldBe("rt");

        fields.TryGetValue("scope", out var redirect_uri).ShouldBeTrue();
        redirect_uri.First().ShouldBe("scope");
    }

    [Fact]
    public async Task Refresh_request_without_refresh_token_should_fail()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions());

        Func<Task> act = async () => await tokenClient.RequestRefreshTokenAsync(refreshToken: null, scope: "scope");

        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.ParamName.ShouldBe("refresh_token");
    }

    [Fact]
    public async Task Setting_no_grant_type_should_fail()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions());

        Func<Task> act = async () => await tokenClient.RequestTokenAsync(grantType: null);

        var exception = await act.ShouldThrowAsync<ArgumentException>();
        exception.ParamName.ShouldBe("grant_type");
    }

    [Fact]
    public async Task Setting_custom_parameters_should_have_correct_format()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions());

        var parameters = new Parameters
        {
            { "client_id", "custom" },
            { "client_secret", "custom" },
            { "custom", "custom" }
        };

        var response = await tokenClient.RequestTokenAsync(grantType: "test", parameters: parameters);

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
    public async Task Mixing_local_and_global_custom_parameters_should_have_correct_format()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions { Parameters = { { "global", "global" } } });

        var parameters = new Parameters
        {
            { "client_id", "custom" },
            { "client_secret", "custom" },
            { "custom", "custom" }
        };

        var response = await tokenClient.RequestTokenAsync(grantType: "test", parameters: parameters);

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

        fields.TryGetValue("global", out var global).ShouldBeTrue();
        global.First().ShouldBe("global");
    }

    [Fact]
    public async Task Local_custom_parameters_should_not_interfere_with_global()
    {
        var globalOptions = new TokenClientOptions { Parameters = { { "global", "value" } } };
        var tokenClient = new TokenClient(_client, globalOptions);

        var localParameters = new Parameters
        {
            { "client_id", "custom" },
            { "client_secret", "custom" },
            { "custom", "custom" }
        };

        _ = await tokenClient.RequestTokenAsync(grantType: "test", parameters: localParameters);

        globalOptions.Parameters.Count.ShouldBe(1);
        var globalValue = globalOptions.Parameters.FirstOrDefault(p => p.Key == "global").Value;
        globalValue.ShouldBe("value");
    }

    [Fact]
    public async Task Setting_basic_authentication_style_should_send_basic_authentication_header()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions
        {
            ClientId = "client",
            ClientSecret = "secret",
            ClientCredentialStyle = ClientCredentialStyle.AuthorizationHeader
        });

        var response = await tokenClient.RequestTokenAsync(grantType: "test");

        var request = _handler.Request;

        request.Headers.Authorization.ShouldNotBeNull();
        request.Headers.Authorization.Scheme.ShouldBe("Basic");
        request.Headers.Authorization.Parameter.ShouldBe(BasicAuthenticationOAuthHeaderValue.EncodeCredential("client", "secret"));
    }

    [Fact]
    public async Task Setting_post_values_authentication_style_should_post_values()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions
        {
            ClientId = "client",
            ClientSecret = "secret",
            ClientCredentialStyle = ClientCredentialStyle.PostBody
        });

        var response = await tokenClient.RequestTokenAsync(grantType: "test");

        var request = _handler.Request;
        request.Headers.Authorization.ShouldBeNull();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields["client_id"].First().ShouldBe("client");
        fields["client_secret"].First().ShouldBe("secret");

    }

    [Fact]
    public async Task Setting_client_id_only_and_post_should_put_client_id_in_post_body()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions
        {
            ClientId = "client",
            ClientCredentialStyle = ClientCredentialStyle.PostBody
        });

        var response = await tokenClient.RequestTokenAsync(grantType: "test");

        var request = _handler.Request;

        request.Headers.Authorization.ShouldBeNull();

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields["client_id"].First().ShouldBe("client");
    }

    [Fact]
    public async Task Setting_client_id_only_and_header_should_put_client_id_in_header()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions
        {
            ClientId = "client",
            ClientCredentialStyle = ClientCredentialStyle.AuthorizationHeader
        });

        var response = await tokenClient.RequestTokenAsync(grantType: "test");

        var request = _handler.Request;

        request.Headers.Authorization.ShouldNotBeNull();
        request.Headers.Authorization.Scheme.ShouldBe("Basic");
        request.Headers.Authorization.Parameter.ShouldBe(BasicAuthenticationOAuthHeaderValue.EncodeCredential("client", ""));

        var fields = QueryHelpers.ParseQuery(_handler.Body);
        fields.TryGetValue("client_secret", out _).ShouldBeFalse();
        fields.TryGetValue("client_id", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Setting_client_id_and_assertion_should_have_correct_format()
    {
        var tokenClient = new TokenClient(_client, new TokenClientOptions
        {
            ClientId = "client",
            ClientAssertion = { Type = "type", Value = "value" }
        });

        var response = await tokenClient.RequestTokenAsync(grantType: "test");
        var fields = QueryHelpers.ParseQuery(_handler.Body);

        fields["grant_type"].First().ShouldBe("test");
        fields["client_id"].First().ShouldBe("client");
        fields["client_assertion_type"].First().ShouldBe("type");
        fields["client_assertion"].First().ShouldBe("value");
    }
}
