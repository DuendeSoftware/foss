// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;

namespace Duende.AccessTokenManagement.Internal;


/// <summary>
/// An <see cref="AccessTokenRequestHandler.ITokenRetriever" /> implementation that retrieves a token using the client credentials flow.
/// </summary>
internal class ClientCredentialsTokenRetriever(
    IClientCredentialsTokenManager clientCredentialsTokenManager,
    ClientName tokenClientName
) : AccessTokenRequestHandler.ITokenRetriever
{
    /// <inheritdoc />
    public async Task<TokenResult<AccessTokenRequestHandler.IToken>> GetTokenAsync(HttpRequestMessage request, CT ct)
    {
        var parameters = new TokenRequestParameters
        {
            ForceTokenRenewal = request.GetForceRenewal()
        };
        var getTokenResult = await clientCredentialsTokenManager.GetAccessTokenAsync(tokenClientName, parameters, ct);

        if (getTokenResult.WasSuccessful(out var token, out var error))
        {
            return token;
        }

        return error;
    }
}
