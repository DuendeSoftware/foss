// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.DPoP;
using Newtonsoft.Json.Linq;
using Serilog;

namespace NetCoreConsoleClient;

public static class Configuration
{
    public static string Authority = "https://demo.duendesoftware.com";
    public static string ClientId = "interactive.confidential.jwt.dpop";
    public static string Api = "https://demo.duendesoftware.com/api/test";
}

public class Program
{


    static HttpClient _apiClient = new HttpClient { BaseAddress = new Uri(Configuration.Api) };

    public static void Main(string[] args) => MainAsync().GetAwaiter().GetResult();

    public static async Task MainAsync()
    {
        Console.WriteLine("+-----------------------+");
        Console.WriteLine("|  Sign in with OIDC    |");
        Console.WriteLine("+-----------------------+");
        Console.WriteLine("");
        Console.WriteLine("Press any key to sign in...");
        // Console.ReadKey();

        await Login();
    }

    private static async Task Login()
    {
        // create a redirect URI using an available port on the loopback address.
        // requires the OP to allow random ports on 127.0.0.1 - otherwise set a static port
        var browser = new SystemBrowser();
        var redirectUri = string.Format($"http://127.0.0.1:{browser.Port}");

        var options = new OidcClientOptions
        {
            Authority = Configuration.Authority,
            ClientId = Configuration.ClientId,
            GetClientAssertionAsync = async () => await ClientAssertionService.Create(),
            RedirectUri = redirectUri,
            Scope = "openid profile api",
            FilterClaims = false,
            Browser = browser,
        };
        var proofKey = JsonWebKeys.CreateRsaJson();
        options.ConfigureDPoP(proofKey);

        var serilog = new LoggerConfiguration()
            .MinimumLevel.Error()
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message}{NewLine}{Exception}{NewLine}")
            .CreateLogger();

        options.LoggerFactory.AddSerilog(serilog);

        var oidcClient = new OidcClient(options);
        var result = await oidcClient.LoginAsync(new LoginRequest());

        ShowResult(result);
        await NextSteps(result, oidcClient);
    }

    private static void ShowResult(LoginResult result)
    {
        if (result.IsError)
        {
            Console.WriteLine("\n\nError:\n{0}", result.Error);
            return;
        }

        Console.WriteLine("\n\nClaims:");
        foreach (var claim in result.User.Claims)
        {
            Console.WriteLine("{0}: {1}", claim.Type, claim.Value);
        }

        Console.WriteLine($"\nidentity token: {result.IdentityToken}");
        Console.WriteLine($"access token:   {result.AccessToken}");
        Console.WriteLine($"refresh token:  {result?.RefreshToken ?? "none"}");
    }

    private static async Task NextSteps(LoginResult result, OidcClient oidcClient)
    {
        var currentAccessToken = result.AccessToken;
        var currentRefreshToken = result.RefreshToken;

        var menu = "  x...exit  c...call api   ";
        if (currentRefreshToken != null) menu += "r...refresh token   ";

        while (true)
        {
            Console.WriteLine("\n\n");

            Console.Write(menu);
            var key = Console.ReadKey();

            if (key.Key == ConsoleKey.X) return;
            if (key.Key == ConsoleKey.C) await CallApi(currentAccessToken);
            if (key.Key == ConsoleKey.R)
            {
                var refreshResult = await oidcClient.RefreshTokenAsync(currentRefreshToken);
                if (refreshResult.IsError)
                {
                    Console.WriteLine($"Error: {refreshResult.Error}");
                }
                else
                {
                    currentRefreshToken = refreshResult.RefreshToken;
                    currentAccessToken = refreshResult.AccessToken;

                    Console.WriteLine("\n\n");
                    Console.WriteLine($"access token:   {refreshResult.AccessToken}");
                    Console.WriteLine($"refresh token:  {refreshResult?.RefreshToken ?? "none"}");
                }
            }
        }
    }

    private static async Task CallApi(string currentAccessToken)
    {
        _apiClient.SetBearerToken(currentAccessToken);
        var response = await _apiClient.GetAsync("");

        if (response.IsSuccessStatusCode)
        {
            var json = JArray.Parse(await response.Content.ReadAsStringAsync());
            Console.WriteLine("\n\n");
            Console.WriteLine(json);
        }
        else
        {
            Console.WriteLine($"Error: {response.ReasonPhrase}");
        }
    }
}
