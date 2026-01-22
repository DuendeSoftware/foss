// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Text.Json;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;

namespace Web;

public class SampleConfiguration
{
    public bool UseDPoP { get; init; }
    public string BaseUrl { get; init; } = null!;
    public string ApiBaseUrl => UseDPoP ? $"{BaseUrl}/api/dpop/" : $"{BaseUrl}/api/";
}

public static class Startup
{
    internal static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        var config = new SampleConfiguration();
        builder.Services.Configure<SampleConfiguration>(builder.Configuration);
        builder.Configuration.Bind(config);

        builder.Services.AddControllersWithViews();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = "cookie";
                options.DefaultChallengeScheme = "oidc";
            })
            .AddCookie("cookie", options =>
            {
                options.Cookie.Name = "web";

                options.Events.OnSigningOut = async e => { await e.HttpContext.RevokeRefreshTokenAsync(); };
            })
            .AddOpenIdConnect("oidc", options =>
            {
                options.Authority = config.BaseUrl;

                options.ClientId = "interactive.confidential.short";
                options.ClientSecret = "secret";

                options.ResponseType = "code";
                options.ResponseMode = "query";

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("offline_access");
                options.Scope.Add("api");
                options.Scope.Add("resource1.scope1");

                options.Resource = "urn:resource1";

                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });

        var rsaKey = new RsaSecurityKey(RSA.Create(2048));
        var jsonWebKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaKey);
        jsonWebKey.Alg = "PS256";
        var jwk = JsonSerializer.Serialize(jsonWebKey) ?? throw new InvalidOperationException("Failed to deserialize");

        builder.Services.AddSingleton<ITokenRequestCustomizer, MyTokenRequestCustomizer>();

        builder.Services.AddOpenIdConnectAccessTokenManagement(options =>
        {
            var useDPoP = builder.Configuration.GetValue<bool>("UseDPoP");
            options.DPoPJsonWebKey = useDPoP ? DPoPProofKey.ParseOrDefault(jwk) : null;
        });

        // registers HTTP client that uses the managed user access token
        builder.Services.AddUserAccessTokenHttpClient("user",
            configureClient: client =>
            {
                client.BaseAddress = new Uri(config.ApiBaseUrl);
            });

        // registers HTTP client that uses the managed user access token and
        // includes a resource indicator
        builder.Services.AddUserAccessTokenHttpClient("user-resource",
            new UserTokenRequestParameters
            {
                Resource = Resource.Parse("urn:resource1")
            },
            configureClient: client =>
            {
                client.BaseAddress = new Uri(config.ApiBaseUrl);
            });

        // registers HTTP client that uses the managed client access token
        builder.Services.AddClientAccessTokenHttpClient("client",
            configureClient: client => { client.BaseAddress = new Uri(config.ApiBaseUrl); });

        // registers HTTP client that uses the managed client access token and
        // includes a resource indicator
        builder.Services.AddClientAccessTokenHttpClient("client-resource",
            new UserTokenRequestParameters
            {
                Resource = Resource.Parse("urn:resource1")
            },
            configureClient: client => { client.BaseAddress = new Uri(config.ApiBaseUrl); });

        // registers a typed HTTP client with token management support
        builder.Services.AddHttpClient<TypedUserClient>(client =>
            {
                client.BaseAddress = new Uri(config.ApiBaseUrl);
            })
            .AddUserAccessTokenHandler();

        builder.Services.AddHttpClient<TypedClientClient>(client =>
            {
                client.BaseAddress = new Uri(config.ApiBaseUrl);
            })
            .AddClientAccessTokenHandler();

        return builder.Build();
    }

    internal static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseSerilogRequestLogging(
            options => options.GetLevel = (httpContext, elapsed, ex) => LogEventLevel.Debug);

        app.UseDeveloperExceptionPage();
        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapDefaultControllerRoute()
            .RequireAuthorization();

        return app;
    }
}

internal class MyTokenRequestCustomizer(IHttpContextAccessor context) : ITokenRequestCustomizer
{
    public async Task<TokenRequestParameters> Customize(HttpRequestContext httpRequest, TokenRequestParameters baseParameters,
        CancellationToken cancellationToken = default)
    {
        // you can add the id token as the id token hint to the token request as follows:
        var idToken = await context.HttpContext!.GetTokenAsync("id_token");
        baseParameters.Parameters.Add("id_token_hint", idToken ?? "");

        // you can also get the access token here, 
        var accessToken = await context.HttpContext!.GetTokenAsync("access_token");
        return baseParameters;
    }
}
