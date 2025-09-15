// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.DPoP;

namespace Duende.IdentityModel.Tests;

public class DPoPExtensionTests
{
    [Theory]
    [InlineData("DPoP-Nonce")]
    [InlineData("dpop-nonce")]
    [InlineData("DPOP-NONCE")]
    public void GetDPoPNonceIsCaseInsensitive(string headerName)
    {
        var expected = "expected-server-nonce";
        var message = new HttpResponseMessage()
        {
            Headers =
            {
                { headerName, expected }
            }
        };
        message.GetDPoPNonce().ShouldBe(expected);
    }

    [Fact]
    public void SetDPoPProofTokenSetsHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var proofToken = "test-proof-token";
        
        request.SetDPoPProofToken(proofToken);
        
        request.Headers.GetValues(OidcConstants.HttpHeaders.DPoP).Single().ShouldBe(proofToken);
    }

    [Fact]
    public void SetDPoPProofTokenClearsExistingHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        request.Headers.Add(OidcConstants.HttpHeaders.DPoP, "old-token");
        
        var newToken = "new-proof-token";
        request.SetDPoPProofToken(newToken);
        
        request.Headers.GetValues(OidcConstants.HttpHeaders.DPoP).Single().ShouldBe(newToken);
    }

    [Fact]
    public void GetDPoPUrlReturnsUrlWithoutQuery()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/path?query=value&other=param");
        
        var result = request.GetDPoPUrl();
        
        result.ShouldBe("https://example.com/path");
    }

    [Fact]
    public void SetDPoPProofTokenWithNullDoesNotSetHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        
        request.SetDPoPProofToken(null);
        
        request.Headers.Contains(OidcConstants.HttpHeaders.DPoP).ShouldBeFalse();
    }

    [Fact]
    public void SetDPoPProofTokenWithEmptyStringDoesNotSetHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        
        request.SetDPoPProofToken("");
        
        request.Headers.Contains(OidcConstants.HttpHeaders.DPoP).ShouldBeFalse();
    }
}