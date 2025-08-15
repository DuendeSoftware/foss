// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using System.Web;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;

namespace Duende.AccessTokenManagement.Framework;

public class AppHost : GenericHost
{
    public string ClientId;
    public string? ClientSecret;

    private readonly IdentityServerHost _identityServerHost;
    private readonly ApiHost _apiHost;
    private readonly Action<UserTokenManagementOptions>? _configureUserTokenManagementOptions;

    public AppHost(
        WriteTestOutput writeTestOutput,
        IdentityServerHost identityServerHost,
        ApiHost apiHost,
        string clientId,
        string baseAddress = "https://app",
        Action<UserTokenManagementOptions>? configureUserTokenManagementOptions = default)
        : base(writeTestOutput, baseAddress)
    {
        _identityServerHost = identityServerHost;
        _apiHost = apiHost;
        ClientId = clientId;
        ClientSecret = "secret";
        _configureUserTokenManagementOptions = configureUserTokenManagementOptions;
        OnConfigureServices += ConfigureServices;
        OnConfigure += Configure;
    }

    public MockHttpMessageHandler? IdentityServerHttpHandler { get; set; }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddRouting();
        services.AddAuthorization();

        services.AddAuthentication("cookie")
            .AddCookie("cookie", options =>
            {
                options.Cookie.Name = "bff";
            });

        services.AddAuthentication(options =>
            {
                options.DefaultChallengeScheme = "oidc";
                options.DefaultSignOutScheme = "oidc";
            })
            .AddOpenIdConnect("oidc", options =>
            {
                options.Events.OnRedirectToIdentityProviderForSignOut = async e =>
                {
                    await e.HttpContext.RevokeRefreshTokenAsync();
                };

                options.Authority = _identityServerHost.Url();

                options.ClientId = ClientId;
                options.ClientSecret = ClientSecret;
                options.ResponseType = "code";
                options.ResponseMode = "query";

                options.MapInboundClaims = false;
                options.GetClaimsFromUserInfoEndpoint = false;
                options.SaveTokens = true;

                options.Scope.Clear();
                var client = _identityServerHost.Clients.Single(x => x.ClientId == ClientId);
                foreach (var scope in client.AllowedScopes)
                {
                    options.Scope.Add(scope);
                }

                if (client.AllowOfflineAccess)
                {
                    options.Scope.Add("offline_access");
                }

                var identityServerHandler = _identityServerHost.Server.CreateHandler();
                if (IdentityServerHttpHandler != null)
                {
                    // allow discovery document
                    IdentityServerHttpHandler.When("/.well-known/*")
                        .Respond(identityServerHandler);

                    options.BackchannelHttpHandler = IdentityServerHttpHandler;
                }
                else
                {
                    options.BackchannelHttpHandler = identityServerHandler;
                }

                options.ProtocolValidator.RequireNonce = false;
            });

        services.AddOpenIdConnectAccessTokenManagement(opt =>
        {
            opt.UseChallengeSchemeScopedTokens = true;

            if (_configureUserTokenManagementOptions != null)
            {
                _configureUserTokenManagementOptions(opt);
            }
        });

        services.AddUserAccessTokenHttpClient("callApi", configureClient: client =>
        {
            client.BaseAddress = new Uri(_apiHost.Url());
        })
        .ConfigurePrimaryHttpMessageHandler(() => _apiHost.HttpMessageHandler);
    }

    private void Configure(IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseRouting();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/login", async context =>
            {
                await context.ChallengeAsync(new AuthenticationProperties
                {
                    RedirectUri = "/"
                });
            });

            endpoints.MapGet("/logout", async context =>
            {
                await context.SignOutAsync();
            });

            endpoints.MapGet("/user_token", async context =>
            {
                var token = await context.GetUserAccessTokenAsync().GetToken();
                await context.Response.WriteAsJsonAsync(UserTokenModel.BuildFrom(token));
            });
            endpoints.MapGet("/user", async context =>
            {
                await context.Response.WriteAsJsonAsync(context.User.Claims.ToDictionary(x => x.Type, x => x.Value));
            });
            endpoints.MapGet("/user_token_error", async context =>
            {
                var getResult = await context.GetUserAccessTokenAsync();

                if (getResult.Succeeded)
                {
                    throw new InvalidOperationException("Expected error");
                }

                await context.Response.WriteAsJsonAsync(getResult.FailedResult);
            });

            endpoints.MapGet("/call_api", async (IHttpClientFactory factory, HttpContext _) =>
            {
                var http = factory.CreateClient("callApi");
                var response = await http.GetAsync("test");
                return await response.Content.ReadFromJsonAsync<TokenEchoResponse>();
            });

            endpoints.MapGet("/user_token_with_resource/{resource}", async (string resource, HttpContext context) =>
            {
                var token = await context.GetUserAccessTokenAsync(new UserTokenRequestParameters
                {
                    Resource = Resource.Parse(resource)
                }).GetToken();
                await context.Response.WriteAsJsonAsync(UserTokenModel.BuildFrom(token));
            });

            endpoints.MapGet("/client_token", async context =>
            {
                var token = await context.GetClientAccessTokenAsync().GetToken();
                await context.Response.WriteAsJsonAsync(ClientCredentialsTokenModel.BuildFrom(token));
            });

            endpoints.MapGet("/client_token_error", async context =>
            {
                var getResult = await context.GetClientAccessTokenAsync();

                if (getResult.Succeeded)
                {
                    throw new InvalidOperationException("Expected error");
                }
                await context.Response.WriteAsJsonAsync(getResult.FailedResult);
            });
        });
    }

    public async Task<HttpResponseMessage> LoginAsync(string sub, string? sid = null, bool verifyDpopThumbprintSent = false)
    {
        await _identityServerHost.CreateIdentityServerSessionCookieAsync(sub, sid);
        return await OidcLoginAsync(verifyDpopThumbprintSent);
    }

    public async Task<HttpResponseMessage> OidcLoginAsync(bool verifyDpopThumbprintSent)
    {
        var response = await BrowserClient.GetAsync(Url("/login"));
        response.StatusCode.ShouldBe((HttpStatusCode)302); // authorize
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(_identityServerHost.Url("/connect/authorize"));

        if (verifyDpopThumbprintSent)
        {
            var queryParams = HttpUtility.ParseQueryString(response.Headers.Location.Query);
            queryParams.AllKeys.ShouldContain(OidcConstants.AuthorizeRequest.DPoPKeyThumbprint);
        }

        response = await _identityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
        response.StatusCode.ShouldBe((HttpStatusCode)302); // client callback
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(Url("/signin-oidc"));

        response = await BrowserClient.GetAsync(response.Headers.Location.ToString());
        response.StatusCode.ShouldBe((HttpStatusCode)302); // root
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldBe("/");

        response = await BrowserClient.GetAsync(Url(response.Headers.Location.ToString()));
        return response;
    }

    public async Task<HttpResponseMessage> LogoutAsync(string? sid = null)
    {
        var response = await BrowserClient.GetAsync(Url("/logout") + "?sid=" + sid);
        response.StatusCode.ShouldBe((HttpStatusCode)302); // endsession
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(_identityServerHost.Url("/connect/endsession"));

        response = await _identityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
        response.StatusCode.ShouldBe((HttpStatusCode)302); // logout
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(_identityServerHost.Url("/account/logout"));

        response = await _identityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
        response.StatusCode.ShouldBe((HttpStatusCode)302); // post logout redirect uri
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(Url("/signout-callback-oidc"));

        response = await BrowserClient.GetAsync(response.Headers.Location.ToString());
        response.StatusCode.ShouldBe((HttpStatusCode)302); // root

        response = await BrowserClient.GetAsync(Url(response.Headers.Location!.ToString()));
        return response;
    }
}

public class UserTokenModel
{
    public static UserTokenModel BuildFrom(UserToken token) => new()
    {
        AccessToken = token.AccessToken.ToString(),
        DPoPJsonWebKey = token.DPoPJsonWebKey?.ToString(),
        Expiration = token.Expiration,
        Scope = token.Scope?.ToString(),
        ClientId = token.ClientId.ToString(),
        AccessTokenType = token.AccessTokenType?.ToString(),
        RefreshToken = token.RefreshToken?.ToString(),
        IdentityToken = token.IdentityToken?.ToString()
    };
    public string? AccessToken { get; init; }
    public string? DPoPJsonWebKey { get; init; }
    public DateTimeOffset Expiration { get; init; }
    public string? Scope { get; init; }
    public string? ClientId { get; init; }
    public string? AccessTokenType { get; init; }
    public string? RefreshToken { get; init; }
    public string? IdentityToken { get; init; }

}

public class ClientCredentialsTokenModel
{
    public static ClientCredentialsTokenModel BuildFrom(ClientCredentialsToken token) => new()
    {
        AccessToken = token.AccessToken.ToString(),
        DPoPJsonWebKey = token.DPoPJsonWebKey?.ToString(),
        Expiration = token.Expiration,
        Scope = token.Scope?.ToString(),
        ClientId = token.ClientId.ToString(),
        AccessTokenType = token.AccessTokenType?.ToString(),
    };
    public required string AccessToken { get; init; }
    public required string? DPoPJsonWebKey { get; init; }
    public required DateTimeOffset Expiration { get; init; }
    public required string? Scope { get; init; }
    public required string ClientId { get; init; }
    public required string? AccessTokenType { get; init; }

}
