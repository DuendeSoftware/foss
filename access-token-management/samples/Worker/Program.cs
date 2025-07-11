// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.DPoP;
using Microsoft.IdentityModel.Tokens;

namespace WorkerService;

public class Program
{
    public static void Main(string[] args) =>
        //Log.Logger = new LoggerConfiguration()
        //    .MinimumLevel.Debug()
        //    .WriteTo.Console(theme: AnsiConsoleTheme.Code)
        //    .CreateLogger();

        CreateHostBuilder(args).Build().Run();

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)

            .ConfigureServices((services) =>
            {
                services.AddDistributedMemoryCache();

                services.AddClientCredentialsTokenManagement()
                    .AddClient("demo", client =>
                    {
                        client.TokenEndpoint = new Uri("https://demo.duendesoftware.com/connect/token");

                        client.ClientId = ClientId.Parse("m2m.short");
                        client.ClientSecret = ClientSecret.Parse("secret");

                        client.Scope = Scope.Parse("api");
                    })
                    .AddClient("demo.dpop", client =>
                    {
                        client.TokenEndpoint = new Uri("https://demo.duendesoftware.com/connect/token");

                        client.ClientId = ClientId.Parse("m2m.dpop");
                        client.ClientSecret = ClientSecret.Parse("secret");

                        client.Scope = Scope.Parse("api");
                        client.DPoPJsonWebKey = CreateDPoPKey();
                    })
                    .AddClient("demo.jwt", client =>
                    {
                        client.TokenEndpoint = new Uri("https://demo.duendesoftware.com/connect/token");
                        client.ClientId = ClientId.Parse("m2m.short.jwt");

                        client.Scope = Scope.Parse("api");
                    });

                services.AddClientCredentialsHttpClient("client", ClientCredentialsClientName.Parse("demo"), client =>
                {
                    client.BaseAddress = new Uri("https://demo.duendesoftware.com/api/");
                });

                services.AddClientCredentialsHttpClient("client.dpop", ClientCredentialsClientName.Parse("demo.dpop"), client =>
                {
                    client.BaseAddress = new Uri("https://demo.duendesoftware.com/api/dpop/");
                });

                services.AddHttpClient<TypedClient>(client =>
                    {
                        client.BaseAddress = new Uri("https://demo.duendesoftware.com/api/");
                    })
                    .AddClientCredentialsTokenHandler(ClientCredentialsClientName.Parse("demo"));

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
        var jwkJson = JsonSerializer.Serialize(jwk, WorkerSerializationContext.Default.JsonWebKey);
        return DPoPProofKey.Parse(jwkJson);
    }

}
[JsonSerializable(typeof(JsonWebKey))]
internal partial class WorkerSerializationContext : JsonSerializerContext;
