// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddAuthentication("token")
    .AddJwtBearer("token", options =>
    {
        options.Authority = Services.IdentityServer.ActualUri().ToString();
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateAudience = false,
            ValidTypes = new[] { "at+jwt" },

            NameClaimType = "name",
            RoleClaimType = "role"
        };

    });

var services = builder.Services;
services.AddTransient<ChaosMonkeyHandler>();
services.AddDistributedMemoryCache();
services.AddHttpClient().AddHttpClient("c1").AddHttpMessageHandler<ChaosMonkeyHandler>();
services.AddClientCredentialsHttpClient("t2", ClientCredentialsClientName.Parse("c1"));
services.AddClientCredentialsTokenManagement(opt => opt.CacheLifetimeBuffer = 0)
    .AddClient(ClientCredentialsClientName.Parse("c1"), opt =>
    {
        opt.TokenEndpoint = new Uri(Services.IdentityServer.ActualUri(), "/connect/token");
        opt.ClientId = ClientId.Parse("tokenendpoint");
        opt.ClientSecret = ClientSecret.Parse("secret");
        opt.HttpClientName = "c1";
    });

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("cache");
    options.InstanceName = "SampleInstance";
});

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapGet("/client", async (HttpContext c, IHttpClientFactory factory, CancellationToken ct) =>
{
    var client = factory.CreateClient("t2");
    var response = await client.GetAsync($"https://{c.Request.Host}/ok", ct);
    return await response.Content.ReadAsStringAsync(ct);
});

app.MapGet("/ok", () => "ok");

app.MapGet("/token", async (IClientCredentialsTokenManager svc, CancellationToken ct) =>
    await svc.GetAccessTokenAsync(ClientCredentialsClientName.Parse("c1"), ct: ct));

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public class ChaosMonkeyHandler : DelegatingHandler
{
    private static readonly Random Random = new Random();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // 10% chance to return unauthorized
        if (Random.Next(0, 10) == 0)
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
