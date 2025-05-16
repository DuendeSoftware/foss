// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.Types;

public class TokenResultTests
{
    [Fact]
    public void Can_convert_failed_result_to_result()
    {
        var result = TokenResult.Failure("error", "description");
        TokenResult<ClientCredentialsToken> tokenResult = result;

        tokenResult.Succeeded.ShouldBeFalse();
        (!tokenResult.Succeeded).ShouldBeTrue();
        tokenResult.FailedResult.ShouldNotBeNull();
        tokenResult.FailedResult.ShouldBeEquivalentTo(result);
    }

    [Fact]
    public void Can_implicitly_convert_success_result_to_result()
    {
        var result = new ClientCredentialsToken()
        {
            ClientId = ClientId.Parse("client_id"),
            AccessToken = AccessTokenString.Parse("access_token"),
            AccessTokenType = AccessTokenType.Parse("type"),
            DPoPJsonWebKey = null,
            Expiration = DateTimeOffset.UtcNow.AddHours(1),
            Scope = AccessTokenManagement.Scope.Parse("scope")
        };

        TokenResult<ClientCredentialsToken> tokenResult = result;
        tokenResult.Succeeded.ShouldBeTrue();
        (!tokenResult.Succeeded).ShouldBeFalse();
        tokenResult.Token.ShouldNotBeNull();
        tokenResult.Token.ShouldBe(result);
    }
}
