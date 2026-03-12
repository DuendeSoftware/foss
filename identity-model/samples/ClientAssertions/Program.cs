// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;



var disco = await GetDiscoveryDocument();

// HttpClient Extensions style
Console.WriteLine("Requesting tokens using HttpClient Extension Method");
var http = new HttpClient();
var response = await http.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest()
{
    Address = disco.TokenEndpoint ?? throw new InvalidOperationException("No token endpoint found in discovery document"),
    ClientId = "m2m.jwt",
    ClientAssertion = await ClientAssertionService.Create(),
    ClientAssertionFactory = async () => await ClientAssertionService.Create(),
    ClientCredentialStyle = ClientCredentialStyle.PostBody
});
Display(response);


async Task<DiscoveryDocumentResponse> GetDiscoveryDocument()
{
    var http = new HttpClient()
    {
        BaseAddress = new Uri("https://demo.duendesoftware.com")
    };
    var disco = await http.GetDiscoveryDocumentAsync();
    if (disco.IsError)
    {
        Console.WriteLine("Discovery failure: " + disco.Error);
        Environment.Exit(-1);
    }
    return disco;
}

void Display(TokenResponse tokenResponse)
{
    if (tokenResponse.IsError)
    {
        Console.WriteLine($"{tokenResponse.Error} - {tokenResponse.ErrorDescription}");
    }
    else
    {
        Console.WriteLine("Access Token received:");
        Console.WriteLine(tokenResponse.AccessToken);
        Console.WriteLine();
    }
}
