// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.DPoP;
using Duende.IdentityModel.OidcClient.DPoP;

namespace Duende.IdentityModel.OidcClient;

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
}
