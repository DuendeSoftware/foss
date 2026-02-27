// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

//
// This sample demonstrates how to use a ClientAssertionFactory so that token
// requests (including DPoP nonce retries) automatically produce a fresh
// client_assertion JWT with a unique jti on each attempt.
//
// Background
// ----------
// When a token request carries a client_assertion (RFC 7521 / private_key_jwt),
// the server may reject retries that reuse the same assertion because the jti
// has already been seen. Setting ClientAssertionFactory on the ProtocolRequest
// ensures that:
//   1. The initial request gets a freshly-minted assertion.
//   2. The factory is stored on HttpRequestMessage.Options so that downstream
//      handlers (e.g. DPoP proof-token handlers) can invoke it on retries.
//

using Duende.IdentityModel.Client;

namespace ClientAssertions;

public class Program
{
    // -----------------------------------------------------------------------
    //  Configuration - adjust these to point to your identity provider.
    // -----------------------------------------------------------------------
    private const string Authority = "https://demo.duendesoftware.com";
    private const string TokenEndpoint = $"{Authority}/connect/token";
    private const string ClientId = "m2m.jwt";
    private const string Scope = "api";

    public static async Task Main()
    {
        Console.WriteLine("+------------------------------------------+");
        Console.WriteLine("|  Client Assertions Sample                |");
        Console.WriteLine("+------------------------------------------+");
        Console.WriteLine();

        // 1. Create a signing key for client_assertion JWTs.
        //    In production this would be the key registered with the identity
        //    provider for private_key_jwt authentication.
        var signingCredentials = ClientAssertionService.CreateSigningCredentials();

        // 2. Create the assertion service.
        var assertionService = new ClientAssertionService(ClientId, Authority, signingCredentials);

        // 3. Build the HTTP client. In a real DPoP scenario you would wrap this
        //    with a ProofTokenMessageHandler, but this sample focuses on the
        //    client assertion factory pattern alone.
        var client = new HttpClient();

        // 4. Build the token request with a ClientAssertionFactory.
        //    The factory is called once for the initial request and stored on
        //    HttpRequestMessage.Options so that retry handlers can call it again.
        var tokenRequest = new ClientCredentialsTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = ClientId,
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
            Scope = Scope,

            // The key feature: a factory that produces a fresh assertion each time.
            ClientAssertionFactory = assertionService.CreateAssertionAsync,
        };

        Console.WriteLine("Requesting token with client_assertion...");
        Console.WriteLine($"  Endpoint : {TokenEndpoint}");
        Console.WriteLine($"  ClientId : {ClientId}");
        Console.WriteLine();

        // 5. Send the token request.
        var response = await client.RequestClientCredentialsTokenAsync(tokenRequest);

        // 6. Display the result.
        if (response.IsError)
        {
            Console.WriteLine($"Error: {response.Error}");
            Console.WriteLine($"Description: {response.ErrorDescription}");

            if (response.HttpStatusCode != 0)
            {
                Console.WriteLine($"HTTP {(int)response.HttpStatusCode}");
            }
        }
        else
        {
            Console.WriteLine("Success!");
            Console.WriteLine($"  Token type   : {response.TokenType}");
            Console.WriteLine($"  Expires in   : {response.ExpiresIn}s");
            Console.WriteLine($"  Access token : {response.AccessToken?[..Math.Min(40, response.AccessToken.Length)]}...");
        }
    }
}
