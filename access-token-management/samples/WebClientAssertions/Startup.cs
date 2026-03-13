// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text.Json;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;

namespace WebClientAssertions;

public static class Startup
{
    internal static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllersWithViews();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = "cookie";
                options.DefaultChallengeScheme = "oidc";
            })
            .AddCookie("cookie", options =>
            {
                options.Cookie.Name = "web-client-assertions";

                options.Events.OnSigningOut = async e => { await e.HttpContext.RevokeRefreshTokenAsync(); };
            })
            .AddOpenIdConnect("oidc", options =>
            {
                options.Authority = "https://demo.duendesoftware.com";

                // Interactive client with JWT client auth + DPoP nonce mode.
                // This client has RequireDPoP = true, DPoPValidationMode = Nonce,
                // and a short (75s) access token lifetime — perfect for demonstrating
                // assertion regeneration on DPoP nonce retries.
                options.ClientId = "interactive.confidential.jwt.dpop";
                // No ClientSecret — we use private_key_jwt via IClientAssertionService

                options.ResponseType = "code";
                options.ResponseMode = "query";

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("offline_access");
                options.Scope.Add("api");

                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });

        // --- DPoP proof key (separate from client assertion signing key) ---
        // Generate a fresh RSA key for DPoP proof tokens. This is NOT the same
        // as the client assertion signing key — DPoP proves sender-constraint of
        // the access token, while the client assertion authenticates the client.
        var dpopRsaKey = new RsaSecurityKey(RSA.Create(2048));
        var dpopJsonWebKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(dpopRsaKey);
        dpopJsonWebKey.Alg = "PS256";
        var dpopJwk = JsonSerializer.Serialize(dpopJsonWebKey);

        builder.Services.AddOpenIdConnectAccessTokenManagement(options =>
        {
            options.DPoPJsonWebKey = DPoPProofKey.Parse(dpopJwk);
        });

        // Register our client assertion service (replaces the default no-op)
        builder.Services.AddTransient<IClientAssertionService, ClientAssertionService>();

        // --- Named M2M client (client credentials with JWT auth, no DPoP) ---
        builder.Services.AddClientCredentialsTokenManagement()
            .AddClient("m2m.jwt", client =>
            {
                client.TokenEndpoint = new Uri("https://demo.duendesoftware.com/connect/token");
                client.ClientId = ClientId.Parse("m2m.jwt");
                // No ClientSecret — assertion service provides credentials
                client.Scope = Scope.Parse("api");
            });

        builder.Services.AddUserAccessTokenHttpClient("user_client",
            configureClient: client =>
            {
                client.BaseAddress = new Uri("https://demo.duendesoftware.com/api/dpop/");
            });

        builder.Services.AddHttpClient<TypedUserClient>(client =>
            {
                client.BaseAddress = new Uri("https://demo.duendesoftware.com/api/dpop/");
            })
            .AddUserAccessTokenHandler();

        builder.Services.AddClientCredentialsHttpClient("client",
            ClientCredentialsClientName.Parse("m2m.jwt"),
            client =>
            {
                client.BaseAddress = new Uri("https://demo.duendesoftware.com/api/");
            });

        builder.Services.AddHttpClient<TypedClientClient>(client =>
            {
                client.BaseAddress = new Uri("https://demo.duendesoftware.com/api/");
            })
            .AddClientCredentialsTokenHandler(ClientCredentialsClientName.Parse("m2m.jwt"));

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
