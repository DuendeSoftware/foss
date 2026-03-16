// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AccessTokenManagement.Framework;

public class IdentityServerHost : GenericHost
{
    public IdentityServerHost(WriteTestOutput writeTestOutput, string baseAddress = "https://identityserver")
        : base(writeTestOutput, baseAddress)
    {
        OnConfigureServices += ConfigureServices;
        OnConfigure += Configure;
    }

    public List<Client> Clients { get; } = [];

    public bool EnablePar { get; set; }

    public List<IdentityResource> IdentityResources { get; } =
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email()
    ];

    public List<ApiScope> ApiScopes { get; } = [];

    public List<ApiResource> ApiResources { get; } =
    [
        new("urn:api1"),
        new("urn:api2")
    ];

    public List<Dictionary<string, string>> CapturedTokenRequests { get; } = [];
    public List<Dictionary<string, string>> CapturedRevocationRequests { get; } = [];
    public List<Dictionary<string, string>> CapturedParRequests { get; } = [];

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddRouting();
        services.AddAuthorization();

        services.AddLogging(logging =>
        {
            logging.AddFilter("Duende", LogLevel.Debug);
        });

        services.AddIdentityServer(options =>
            {
                options.EmitStaticAudienceClaim = true;

                // Artificially low durations to force retries
                options.DPoP.ServerClockSkew = TimeSpan.Zero;
                options.DPoP.ProofTokenValidityDuration = TimeSpan.FromSeconds(1);

                // Disable PAR (this keeps test setup simple, and we don't need to integration test PAR here - it is covered by IdentityServer itself)
                options.Endpoints.EnablePushedAuthorizationEndpoint = EnablePar;
            })
            .AddInMemoryClients(Clients)
            .AddInMemoryIdentityResources(IdentityResources)
            .AddInMemoryApiResources(ApiResources)
            .AddInMemoryApiScopes(ApiScopes)
            .AddJwtBearerClientAuthentication();
    }

    private void Configure(IApplicationBuilder app)
    {
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path == "/connect/token" && ctx.Request.Method == "POST")
            {
                var form = await ctx.Request.ReadFormAsync();
                var capturedData = form.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToString());
                CapturedTokenRequests.Add(capturedData);
            }
            else if (ctx.Request.Path == "/connect/revocation" && ctx.Request.Method == "POST")
            {
                var form = await ctx.Request.ReadFormAsync();
                var capturedData = form.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToString());
                CapturedRevocationRequests.Add(capturedData);
            }
            else if (ctx.Request.Path == "/connect/par" && ctx.Request.Method == "POST")
            {
                var form = await ctx.Request.ReadFormAsync();
                var capturedData = form.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToString());
                CapturedParRequests.Add(capturedData);
            }

            await next();
        });

        app.UseRouting();

        app.UseIdentityServer();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/account/login", _ => Task.CompletedTask);

            endpoints.MapGet("/account/logout", async context =>
            {
                // signout as if the user were prompted
                await context.SignOutAsync();

                var logoutId = context.Request.Query["logoutId"];
                var interaction = context.RequestServices.GetRequiredService<IIdentityServerInteractionService>();

                var signOutContext = await interaction.GetLogoutContextAsync(logoutId);

                context.Response.Redirect(signOutContext.PostLogoutRedirectUri ?? "/");
            });
        });
    }

    public async Task CreateIdentityServerSessionCookieAsync(string sub, string? sid = null)
    {
        var props = new AuthenticationProperties();

        if (!string.IsNullOrWhiteSpace(sid))
        {
            props.Items.Add("session_id", sid);
        }

        await IssueSessionCookieAsync(props, new Claim("sub", sub));
    }

    public string CreateIdToken(string sub, string clientId)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = BaseAddress,
            Audience = clientId,
            Claims = new Dictionary<string, object>
            {
                { "sub", sub }
            }
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(descriptor);
    }
}
