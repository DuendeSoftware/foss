// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Duende.AccessTokenManagement.Framework;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
using OidcConstants = Duende.IdentityModel.OidcConstants;

namespace Duende.AccessTokenManagement;

public class UserTokenManagementTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    [Fact]
    public async Task Anonymous_user_should_return_user_token_error()
    {
        await InitializeAsync();
        var response = await AppHost.BrowserClient!.GetAsync(AppHost.Url("/user_token_error"));
        var token = await response.Content.ReadFromJsonAsync<FailedResult>();

        token!.Error.ShouldNotBeNull();
    }

    [Fact]
    public async Task Anonymous_user_should_return_client_token()
    {
        await InitializeAsync();
        var response = await AppHost.BrowserClient!.GetAsync(AppHost.Url("/client_token"));
        var token = await response.Content.ReadFromJsonAsync<ClientCredentialsTokenModel>();

        token!.AccessToken.ShouldNotBeNull();
        token.AccessTokenType.ShouldNotBeNull().ShouldBe("Bearer");
        token.Expiration.ShouldNotBe(DateTimeOffset.MaxValue);
    }

    [Fact]
    public async Task Can_implement_custom_user_principal_transform()
    {
        var mockHttp = new MockHttpMessageHandler();
        AppHost.IdentityServerHttpHandler = mockHttp;

        // Respond to code flow with a short token lifetime so that we trigger refresh on 1st use
        var initialTokenResponse = new
        {
            id_token = IdentityServerHost.CreateIdToken("1", "web"),
            access_token = "initial_access_token",
            token_type = "tokentype",
            expires_in = 10,
            refresh_token = "initial_refresh_token",
        };
        mockHttp.When("/connect/token")
            .Respond("application/json", JsonSerializer.Serialize(initialTokenResponse));

        var transformed = false;

        Task<ClaimsPrincipal> LocalTransformPrincipalAsync(ClaimsPrincipal principal, CancellationToken ct)
        {
            transformed = true;
            return Task.FromResult(new ClaimsPrincipal(
                new ClaimsIdentity([
                    new Claim(JwtClaimTypes.Name, "transformed"),
                ], "openid")));
        }

        AppHost.OnConfigureServices += services =>
        {
            services.AddSingleton<TransformPrincipalAfterRefreshAsync>(LocalTransformPrincipalAsync);
        };
        await InitializeAsync();

        await AppHost.LoginAsync("alice");
        // Get a user token. This should trigger a token refresh, which then get's stored and triggers
        // the custom token transform
        await AppHost.BrowserClient!.GetAsync(AppHost.Url("/user_token"));

        // Verify that the transform is used.
        transformed.ShouldBeTrue();

        // The transformed principal should now be used.
        var claims = await AppHost.BrowserClient!.GetFromJsonAsync<Dictionary<string, string>>(AppHost.Url("/user"));
        claims![JwtClaimTypes.Name].ShouldBe("transformed");
    }

    [Fact]
    public async Task Standard_initial_token_response_should_return_expected_values()
    {
        var mockHttp = new MockHttpMessageHandler();
        AppHost.IdentityServerHttpHandler = mockHttp;

        var initialTokenResponse = new
        {
            id_token = IdentityServerHost.CreateIdToken("1", "web"),
            access_token = "initial_access_token",
            token_type = "tokentype",
            expires_in = 3600,
            refresh_token = "initial_refresh_token",
        };

        // response for re-deeming code
        mockHttp.When("/connect/token")
            .WithFormData("grant_type", "authorization_code")
            .Respond("application/json", JsonSerializer.Serialize(initialTokenResponse));

        await InitializeAsync();
        await AppHost.LoginAsync("alice");

        // 1st request
        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserTokenModel>();

        token.ShouldNotBeNull();
        token.AccessToken.ShouldBe("initial_access_token");
        token.AccessTokenType.ShouldNotBeNull().ShouldBe("tokentype");
        token.RefreshToken.ShouldNotBeNull().ShouldBe("initial_refresh_token");
        token.Expiration.ShouldNotBe(DateTimeOffset.MaxValue);

        // 2nd request should not trigger a token request
        response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/user_token"));
        token = await response.Content.ReadFromJsonAsync<UserTokenModel>();

        token.ShouldNotBeNull();
        token.AccessToken.ShouldBe("initial_access_token");
        token.AccessTokenType.ShouldNotBeNull().ShouldBe("tokentype");
        token.RefreshToken.ShouldNotBeNull().ShouldBe("initial_refresh_token");
        token.Expiration.ShouldNotBe(DateTimeOffset.MaxValue);
    }

    [Fact]
    public async Task Missing_expires_in_should_result_in_long_lived_token()
    {
        var mockHttp = new MockHttpMessageHandler();
        AppHost.IdentityServerHttpHandler = mockHttp;

        var initialTokenResponse = new
        {
            id_token = IdentityServerHost.CreateIdToken("1", "web"),
            access_token = "initial_access_token",
            token_type = "tokentype",
            refresh_token = "initial_refresh_token",
        };

        // response for re-deeming code
        mockHttp.When("/connect/token")
            .WithFormData("grant_type", "authorization_code")
            .Respond("application/json", JsonSerializer.Serialize(initialTokenResponse));

        await InitializeAsync();
        await AppHost.LoginAsync("alice");

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserTokenModel>();

        token.ShouldNotBeNull();
        token.AccessToken.ShouldBe("initial_access_token");
        token.AccessTokenType.ShouldNotBeNull().ShouldBe("tokentype");
        token.RefreshToken.ShouldNotBeNull().ShouldBe("initial_refresh_token");
        token.Expiration.ShouldBe(DateTimeOffset.MaxValue);
    }

    [Fact]
    public async Task Missing_initial_refresh_token_response_should_return_access_token()
    {
        var mockHttp = new MockHttpMessageHandler();
        AppHost.IdentityServerHttpHandler = mockHttp;

        var initialTokenResponse = new
        {
            id_token = IdentityServerHost.CreateIdToken("1", "web"),
            access_token = "initial_access_token",
            token_type = "tokentype",
            expires_in = 3600
        };

        // response for re-deeming code
        mockHttp.When("/connect/token")
            .WithFormData("grant_type", "authorization_code")
            .Respond("application/json", JsonSerializer.Serialize(initialTokenResponse));

        await InitializeAsync();
        await AppHost.LoginAsync("alice");

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserTokenModel>();

        token.ShouldNotBeNull();
        token.AccessToken.ShouldBe("initial_access_token");
        token.AccessTokenType.ShouldNotBeNull().ShouldBe("tokentype");
        token.RefreshToken.ShouldBeNull();
        token.Expiration.ShouldNotBe(DateTimeOffset.MaxValue);
    }

    [Fact]
    public async Task Missing_initial_refresh_token_and_expired_access_token_should_return_initial_access_token()
    {
        var mockHttp = new MockHttpMessageHandler();
        AppHost.IdentityServerHttpHandler = mockHttp;

        var initialTokenResponse = new
        {
            id_token = IdentityServerHost.CreateIdToken("1", "web"),
            access_token = "initial_access_token",
            token_type = "tokentype",
            expires_in = 10
        };

        // response for re-deeming code
        mockHttp.When("/connect/token")
            .WithFormData("grant_type", "authorization_code")
            .Respond("application/json", JsonSerializer.Serialize(initialTokenResponse));

        await InitializeAsync();
        await AppHost.LoginAsync("alice");

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserTokenModel>();

        token.ShouldNotBeNull();
        token.AccessToken.ShouldBe("initial_access_token");
        token.AccessTokenType.ShouldNotBeNull().ShouldBe("tokentype");
        token.RefreshToken.ShouldBeNull();
        token.Expiration.ShouldNotBe(DateTimeOffset.MaxValue);
    }

    [Fact]
    public async Task Short_token_lifetime_should_trigger_refresh()
    {
        // This test makes an initial token request using code flow and then
        // refreshes the token a couple of times.

        // We mock the expiration of the first few token responses to be short
        // enough that we will automatically refresh immediately when attempting
        // to use the tokens, while the final response gets a long refresh time,
        // allowing us to verify that the token is not refreshed.

        var mockHttp = new MockHttpMessageHandler();
        AppHost.IdentityServerHttpHandler = mockHttp;

        // Respond to code flow with a short token lifetime so that we trigger refresh on 1st use
        var initialTokenResponse = new
        {
            id_token = IdentityServerHost.CreateIdToken("1", "web"),
            access_token = "initial_access_token",
            token_type = "tokentype",
            expires_in = 10,
            refresh_token = "initial_refresh_token",
        };
        mockHttp.When("/connect/token")
            .WithFormData("grant_type", "authorization_code")
            .Respond("application/json", JsonSerializer.Serialize(initialTokenResponse));

        // Respond to refresh with a short token lifetime so that we trigger another refresh on 2nd use
        var refreshTokenResponse = new
        {
            id_token = "refreshed1_id_token",
            access_token = "refreshed1_access_token",
            token_type = "tokentype1",
            expires_in = 10,
            refresh_token = "refreshed1_refresh_token",
        };
        mockHttp.When("/connect/token")
            .WithFormData("grant_type", "refresh_token")
            .WithFormData("refresh_token", "initial_refresh_token")
            .Respond("application/json", JsonSerializer.Serialize(refreshTokenResponse));

        // Respond to second refresh with a long token lifetime so that we don't trigger another refresh on 3rd use
        var refreshTokenResponse2 = new
        {
            id_token = "refreshed2_id_token",
            access_token = "refreshed2_access_token",
            token_type = "tokentype2",
            expires_in = 3600,
            refresh_token = "refreshed2_refresh_token",
        };
        mockHttp.When("/connect/token")
            .WithFormData("grant_type", "refresh_token")
            .WithFormData("refresh_token", "refreshed1_refresh_token")
            .Respond("application/json", JsonSerializer.Serialize(refreshTokenResponse2));

        // setup host
        await InitializeAsync();
        await AppHost.LoginAsync("alice");

        // first request should trigger refresh
        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserTokenModel>();

        token.ShouldNotBeNull();
        token.IdentityToken.ShouldNotBeNull().ShouldBe("refreshed1_id_token");
        token.AccessToken.ShouldBe("refreshed1_access_token");
        token.AccessTokenType.ShouldNotBeNull().ShouldBe("tokentype1");
        token.RefreshToken.ShouldNotBeNull().ShouldBe("refreshed1_refresh_token");
        token.Expiration.ShouldNotBe(DateTimeOffset.MaxValue);

        // second request should trigger refresh
        response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/user_token"));
        token = await response.Content.ReadFromJsonAsync<UserTokenModel>();

        token.ShouldNotBeNull();
        token.IdentityToken.ShouldNotBeNull().ShouldBe("refreshed2_id_token");
        token.AccessToken.ShouldBe("refreshed2_access_token");
        token.AccessTokenType.ShouldNotBeNull().ShouldBe("tokentype2");
        token.RefreshToken.ShouldNotBeNull().ShouldBe("refreshed2_refresh_token");
        token.Expiration.ShouldNotBe(DateTimeOffset.MaxValue);

        // third request should not trigger refresh
        response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/user_token"));
        token = await response.Content.ReadFromJsonAsync<UserTokenModel>();

        token.ShouldNotBeNull();
        token.AccessToken.ShouldBe("refreshed2_access_token");
        token.AccessTokenType.ShouldNotBeNull().ShouldBe("tokentype2");
        token.RefreshToken.ShouldNotBeNull().ShouldBe("refreshed2_refresh_token");
        token.Expiration.ShouldNotBe(DateTimeOffset.MaxValue);
    }

    [Fact]
    public async Task Resources_get_distinct_tokens()
    {
        var mockHttp = new MockHttpMessageHandler();
        AppHost.IdentityServerHttpHandler = mockHttp;

        // no resource specified
        var initialTokenResponse = new
        {
            id_token = IdentityServerHost.CreateIdToken("1", "web"),
            access_token = "access_token_without_resource",
            token_type = "tokentype",
            expires_in = 3600,
            refresh_token = "initial_refresh_token",
        };
        mockHttp.When("/connect/token")
            .WithFormData("grant_type", "authorization_code")
            .Respond("application/json", JsonSerializer.Serialize(initialTokenResponse));

        // resource 1 specified
        var resource1TokenResponse = new
        {
            access_token = "urn:api1_access_token",
            token_type = "tokentype1",
            expires_in = 3600,
            refresh_token = "initial_refresh_token",
        };
        mockHttp.When("/connect/token")
            .WithFormData("grant_type", "refresh_token")
            .WithFormData("resource", "urn:api1")
            .Respond("application/json", JsonSerializer.Serialize(resource1TokenResponse));

        // resource 2 specified
        var resource2TokenResponse = new
        {
            access_token = "urn:api2_access_token",
            token_type = "tokentype1",
            expires_in = 3600,
            refresh_token = "initial_refresh_token",
        };
        mockHttp.When("/connect/token")
            .WithFormData("grant_type", "refresh_token")
            .WithFormData("resource", "urn:api2")
            .Respond("application/json", JsonSerializer.Serialize(resource2TokenResponse));

        // setup host
        await InitializeAsync();
        await AppHost.LoginAsync("alice");

        // first request - no resource
        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserTokenModel>();

        token.ShouldNotBeNull();
        token.AccessToken.ShouldBe("access_token_without_resource");
        token.RefreshToken.ShouldNotBeNull().ShouldBe("initial_refresh_token");
        token.Expiration.ShouldNotBe(DateTimeOffset.MaxValue);

        // second request - with resource api1
        response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/user_token_with_resource/urn:api1"));
        token = await response.Content.ReadFromJsonAsync<UserTokenModel>();

        token.ShouldNotBeNull();
        token.AccessToken.ShouldBe("urn:api1_access_token");
        token.RefreshToken.ShouldNotBeNull().ShouldBe("initial_refresh_token"); // This doesn't change with resources!
        token.Expiration.ShouldNotBe(DateTimeOffset.MaxValue);

        // third request - with resource api2
        response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/user_token_with_resource/urn:api2"));
        token = await response.Content.ReadFromJsonAsync<UserTokenModel>();

        token.ShouldNotBeNull();
        token.AccessToken.ShouldBe("urn:api2_access_token");
        token.RefreshToken.ShouldNotBeNull().ShouldBe("initial_refresh_token");
        token.Expiration.ShouldNotBe(DateTimeOffset.MaxValue);
    }

    [Fact]
    public async Task Refresh_responses_without_refresh_token_use_old_refresh_token()
    {
        var mockHttp = new MockHttpMessageHandler();
        AppHost.IdentityServerHttpHandler = mockHttp;

        // short token lifetime should trigger refresh on 1st use
        var initialTokenResponse = new
        {
            id_token = IdentityServerHost.CreateIdToken("1", "web"),
            access_token = "initial_access_token",
            token_type = "tokentype",
            expires_in = 10,
            refresh_token = "initial_refresh_token",
        };

        // response for re-deeming code
        mockHttp.When("/connect/token")
            .WithFormData("grant_type", "authorization_code")
            .Respond("application/json", JsonSerializer.Serialize(initialTokenResponse));

        // note lack of refresh_token
        var refreshTokenResponse = new
        {
            access_token = "refreshed1_access_token",
            token_type = "tokentype1",
            expires_in = 3600,
        };

        // response for refresh
        mockHttp.When("/connect/token")
            .WithFormData("grant_type", "refresh_token")
            .WithFormData("refresh_token", "initial_refresh_token")
            .Respond("application/json", JsonSerializer.Serialize(refreshTokenResponse));

        // setup host
        await InitializeAsync();
        await AppHost.LoginAsync("alice");

        // first request should trigger refresh
        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserTokenModel>();

        token.ShouldNotBeNull();
        token.RefreshToken.ShouldNotBeNull().ShouldBe("initial_refresh_token");
    }

    [Fact]
    public async Task Multiple_users_have_distinct_tokens_across_refreshes()
    {
        // setup host
        AppHost.ClientId = "web.short";
        await InitializeAsync();
        await AppHost.LoginAsync("alice");
        var firstResponse = await AppHost.BrowserClient.GetAsync(AppHost.Url("/call_api"));
        var firstToken = await firstResponse.Content.ReadFromJsonAsync<TokenEchoResponse>();
        var secondResponse = await AppHost.BrowserClient.GetAsync(AppHost.Url("/call_api"));
        var secondToken = await secondResponse.Content.ReadFromJsonAsync<TokenEchoResponse>();
        firstToken.ShouldNotBeNull();
        secondToken.ShouldNotBeNull();
        secondToken.sub.ShouldBe(firstToken.sub);
        secondToken.token.ShouldNotBe(firstToken.token);
        await AppHost.LoginAsync("bob");
        var thirdResponse = await AppHost.BrowserClient.GetAsync(AppHost.Url("/call_api"));
        var thirdToken = await thirdResponse.Content.ReadFromJsonAsync<TokenEchoResponse>();
        thirdToken.ShouldNotBeNull();
        thirdToken.sub.ShouldNotBe(secondToken.sub);
        thirdToken.token.ShouldNotBe(firstToken.token);
    }

    [Fact]
    public async Task Logout_should_revoke_refresh_tokens()
    {
        await InitializeAsync();
        await AppHost.LoginAsync("alice");

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserTokenModel>();
        var refreshToken = token?.RefreshToken;

        refreshToken.ShouldNotBeNull();

        var introspectionParams = new TokenIntrospectionRequest
        {
            Token = refreshToken,
            TokenTypeHint = OidcConstants.TokenTypes.RefreshToken,
            ClientId = "web",
            ClientSecret = "secret",
            Address = IdentityServerHost.Url("/connect/introspect")
        };

        var introspectionResponse = await IdentityServerHost.HttpClient.IntrospectTokenAsync(introspectionParams);
        introspectionResponse.ShouldNotBeNull();
        introspectionResponse.IsError.ShouldBeFalse(introspectionResponse.Error);
        introspectionResponse.IsActive.ShouldBeTrue();

        await AppHost.BrowserClient.GetAsync(AppHost.Url("/logout"));

        var postLogoutIntrospectionResponse =
            await IdentityServerHost.HttpClient.IntrospectTokenAsync(introspectionParams);
        postLogoutIntrospectionResponse.ShouldNotBeNull();
        postLogoutIntrospectionResponse.IsError.ShouldBeFalse(introspectionResponse.Error);
        postLogoutIntrospectionResponse.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Can_request_user_token_using_client_assertions()
    {
        var mockHttp = new MockHttpMessageHandler();
        AppHost.IdentityServerHttpHandler = mockHttp;
        AppHost.ClientSecret = null;
        AppHost.OnConfigureServices += services =>
        {
            services.AddSingleton<IClientAssertionService>(
                new TestClientAssertionService("test", "service_type", "service_value"));
            services.PostConfigure<OpenIdConnectOptions>("oidc", options =>
            {
                options.Events.OnAuthorizationCodeReceived = async context =>
                {
                    var clientAssertionService =
                        context.HttpContext.RequestServices.GetRequiredService<IClientAssertionService>();
                    var assertion =
                        await clientAssertionService.GetClientAssertionAsync(
                            ClientCredentialsClientName.Parse("test")) ??
                        throw new InvalidOperationException("Client assertion is null");

                    context.TokenEndpointRequest!.ClientAssertionType = assertion.Type;
                    context.TokenEndpointRequest.ClientAssertion = assertion.Value;
                };
            });
        };
        var expectedRequestFormData = new Dictionary<string, string>
        {
            { OidcConstants.TokenRequest.ClientAssertionType, "service_type" },
            { OidcConstants.TokenRequest.ClientAssertion, "service_value" },
        };
        var initialTokenResponse = new
        {
            id_token = IdentityServerHost.CreateIdToken("1", "web"),
            access_token = "initial_access_token",
            token_type = "clientAssertionsWork",
            expires_in = 3600,
            refresh_token = "initial_refresh_token",
        };

        // response for re-deeming code
        mockHttp.When("/connect/token")
            .WithFormData(expectedRequestFormData)
            .Respond("application/json", JsonSerializer.Serialize(initialTokenResponse));
        await InitializeAsync();
        await AppHost.LoginAsync("alice");

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserTokenModel>();

        token.ShouldNotBeNull();
        token.AccessTokenType.ShouldBe("clientAssertionsWork");
    }

    [Fact]
    public async Task Refresh_token_request_should_include_additional_parameters()
    {
        /*
         * We attempt to refresh the token as soon as we attempt to retrieve
         * it because the token expires in 10 seconds, which is within the 1 minute RefreshBeforeExpiration window.
         */
        AppHost.ClientId = "web.short";

        AppHost.OnConfigureServices += services =>
        {
            services.PostConfigure<UserTokenManagementOptions>(opt =>
            {
                opt.RefreshBeforeExpiration = TimeSpan.FromMinutes(1);
            });
        };

        AppHost.OnConfigure += app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/refresh_token_with_parameters", async context =>
                {
                    var token = await context.GetUserAccessTokenAsync(new UserTokenRequestParameters
                    {
                        Parameters = new Parameters([
                            new KeyValuePair<string, string>("param_name", "param_value")
                        ])
                    }).GetToken();
                    await context.Response.WriteAsJsonAsync(UserTokenModel.BuildFrom(token));
                });
            });
        };

        await InitializeAsync();
        await AppHost.LoginAsync("alice");

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/refresh_token_with_parameters"));
        response.EnsureSuccessStatusCode();

        var refreshTokenRequest = IdentityServerHost.CapturedTokenRequests
            .FirstOrDefault(r => r.TryGetValue("grant_type", out var grantType) && grantType == "refresh_token");

        refreshTokenRequest.ShouldNotBeNull("Expected a refresh token request to be captured");
        refreshTokenRequest.ShouldContainKey("param_name");
        refreshTokenRequest["param_name"].ShouldBe("param_value");
    }

    [Fact]
    public async Task Revoke_refresh_token_request_should_include_additional_parameters()
    {
        AppHost.OnConfigure += app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/revoke_token_with_parameters", async context =>
                {
                    await context.RevokeRefreshTokenAsync(new UserTokenRequestParameters
                    {
                        Parameters = new Parameters([
                            new KeyValuePair<string, string>("param_name", "param_value")
                        ])
                    });
                });
            });
        };

        await InitializeAsync();
        await AppHost.LoginAsync("alice");

        var response = await AppHost.BrowserClient.GetAsync(AppHost.Url("/revoke_token_with_parameters"));
        response.EnsureSuccessStatusCode();

        IdentityServerHost.CapturedRevocationRequests.ShouldNotBeEmpty("Expected a revocation request to be captured");
        var revocationRequest = IdentityServerHost.CapturedRevocationRequests.First();
        revocationRequest.ShouldContainKey("param_name");
        revocationRequest["param_name"].ShouldBe("param_value");
    }
}
