// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;

namespace Duende.AccessTokenManagement;

public class DPoPExtensionTests
{
    [Theory]
    [InlineData("DPoP-Nonce")]
    [InlineData("dpop-nonce")]
    [InlineData("DPOP-NONCE")]
    public void GetDPoPNonceIsCaseInsensitive(string headerName)
    {
        var expected = "expected-server-nonce";
        var message = new HttpResponseMessage
        {
            Headers =
            {
                { headerName, expected }
            }
        };
        message.GetDPoPNonceValue().ShouldNotBeNull()
            .ToString().ShouldBe(expected);
    }
}
