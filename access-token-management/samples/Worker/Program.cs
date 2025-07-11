// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text.Json;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.DPoP;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace WorkerService;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(theme: AnsiConsoleTheme.Code)
            .CreateLogger();

        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog()

            .ConfigureServices((services) =>
            {
                services.AddDistributedMemoryCache();

                var demoClient = ClientCredentialsClientName.Parse("demo");
                var dpopClient = ClientCredentialsClientName.Parse("demo.dpop");
                services.AddClientCredentialsTokenManagement()
                    .AddClient(demoClient, client =>
                    {
                        client.TokenEndpoint = new Uri("https://demo.duendesoftware.com/connect/token");

                        client.ClientId = ClientId.Parse("m2m.short");
                        client.ClientSecret = ClientSecret.Parse("secret");

                        client.Scope = Scope.Parse("api");
                    })
                    .AddClient(dpopClient, client =>
                    {
                        client.TokenEndpoint = new Uri("https://demo.duendesoftware.com/connect/token");

                        client.ClientId = ClientId.Parse("m2m.dpop");
                        client.ClientSecret = ClientSecret.Parse("secret");

                        client.Scope = Scope.Parse("api");
                        client.DPoPJsonWebKey = CreateDPoPKey();
                    })
                    .AddClient(ClientCredentialsClientName.Parse("demo.jwt"), client =>
                    {
                        client.TokenEndpoint = new Uri("https://demo.duendesoftware.com/connect/token");
                        client.ClientId = ClientId.Parse("m2m.short.jwt");

                        client.Scope = Scope.Parse("api");
                    });

                services.AddClientCredentialsHttpClient("client", demoClient, client =>
                {
                    client.BaseAddress = new Uri("https://demo.duendesoftware.com/api/");
                });

                services.AddClientCredentialsHttpClient("client.dpop", dpopClient, client =>
                {
                    client.BaseAddress = new Uri("https://demo.duendesoftware.com/api/dpop/");
                });

                services.AddHttpClient<TypedClient>(client =>
                    {
                        client.BaseAddress = new Uri("https://demo.duendesoftware.com/api/");
                    })
                    .AddClientCredentialsTokenHandler(demoClient);

                services.AddTransient<IClientAssertionService, ClientAssertionService>();

                //services.AddHostedService<WorkerManual>();
                //services.AddHostedService<WorkerManualJwt>();
                //services.AddHostedService<WorkerHttpClient>();
                //services.AddHostedService<WorkerTypedHttpClient>();
                services.AddHostedService<WorkerDPoPHttpClient>();
            });

        return host;
    }

    private static DPoPProofKey CreateDPoPKey()
    {
        var key = new RsaSecurityKey(RSA.Create(2048));
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        jwk.Alg = "PS256";
        var jwkJson = JsonSerializer.Serialize(jwk);
        return DPoPProofKey.Parse(jwkJson);
    }

}
