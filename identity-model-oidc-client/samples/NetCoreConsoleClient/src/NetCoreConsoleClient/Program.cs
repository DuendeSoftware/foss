// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

//
// This sample demonstrates using OidcClient with DPoP and a ClientAssertionFactory
// so that DPoP nonce retries automatically regenerate the client_assertion JWT.
//

using System.Security.Cryptography;
using System.Text.Json;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.DPoP;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace ConsoleClientWithBrowser;

public class Program
{
    static string _authority = "https://demo.duendesoftware.com";
    static string _api = "https://demo.duendesoftware.com/api/test";

    static HttpClient _apiClient = new HttpClient { BaseAddress = new Uri(_api) };

    public static void Main(string[] args) => MainAsync().GetAwaiter().GetResult();

    public static async Task MainAsync()
    {
        Console.WriteLine("+-----------------------------------------+");
        Console.WriteLine("|  Sign in with OIDC + DPoP + Assertion   |");
        Console.WriteLine("+-----------------------------------------+");
        Console.WriteLine("");
        Console.WriteLine("Press any key to sign in...");
        Console.ReadKey();

        await Login();
    }

    private static async Task Login()
    {
        // create a redirect URI using an available port on the loopback address.
        var browser = new SystemBrowser();
        string redirectUri = string.Format($"http://127.0.0.1:{browser.Port}");

        // Create a DPoP proof key
        var dpopKey = CreateDPoPProofKey();

        // Create a client assertion signing key and service
        var signingCredentials = ClientAssertionService.CreateSigningCredentials();

        var options = new OidcClientOptions
        {
            Authority = _authority,
            ClientId = "interactive.public",
            RedirectUri = redirectUri,
            Scope = "openid profile api",
            FilterClaims = false,
            Browser = browser,
        };

        // Wire up DPoP
        options.ConfigureDPoP(dpopKey);

        // Wire up the client assertion factory.
        // The factory produces a fresh JWT (with unique jti) on each invocation,
        // which ensures DPoP nonce retries don't replay a stale assertion.
        var assertionService = new ClientAssertionService(
            clientId: options.ClientId,
            audience: _authority,
            signingCredentials: signingCredentials);
        options.GetClientAssertionAsync = assertionService.CreateAssertionAsync;

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

    /// <summary>
    /// Creates a DPoP proof key (RSA, serialized as JWK JSON).
    /// In production, persist this key so the same key is used across restarts.
    /// </summary>
    private static string CreateDPoPProofKey()
    {
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa);
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        jwk.Alg = SecurityAlgorithms.RsaSha256;
        return JsonSerializer.Serialize(jwk);
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
            var json = await response.Content.ReadAsStringAsync();
            Console.WriteLine("\n\n");
            Console.WriteLine(json);
        }
        else
        {
            Console.WriteLine($"Error: {response.ReasonPhrase}");
        }
    }
}
