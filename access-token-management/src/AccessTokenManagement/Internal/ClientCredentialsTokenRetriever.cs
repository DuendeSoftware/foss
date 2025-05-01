// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OTel;
using Duende.AccessTokenManagement.Types;

namespace Duende.AccessTokenManagement.Internal;


/// <summary>
/// An <see cref="IAccessTokenHandlerTokenRetriever" /> implementation that retrieves a token using the client credentials flow.
/// </summary>
internal class ClientCredentialsTokenRetriever(
    IClientCredentialsTokenManagementService clientCredentialsTokenManager,
    ClientCredentialsClientName tokenClientName
) : AccessTokenRequestHandler.ITokenRetriever
{
    /// <inheritdoc />
    public async Task<TokenResult<AccessTokenRequestHandler.IToken>> GetToken(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var parameters = new TokenRequestParameters
        {
            ForceTokenRenewal = request.GetForceRenewal()
        };
        var getTokenResult = await clientCredentialsTokenManager.GetAccessTokenAsync(tokenClientName, parameters, cancellationToken);

        if (getTokenResult.WasSuccessful(out var token, out var error))
        {
            return token;
        }

        return error;
    }

    /// <inheritdoc />
    public AccessTokenManagementMetrics.TokenRequestType TokenRequestType { get; } = AccessTokenManagementMetrics.TokenRequestType.ClientCredentials;
}
