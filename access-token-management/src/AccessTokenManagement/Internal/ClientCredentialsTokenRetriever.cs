// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OTel;

namespace Duende.AccessTokenManagement.Internal;


/// <summary>
/// An <see cref="ITokenRetriever" /> implementation that retrieves a token using the client credentials flow.
/// </summary>
internal class ClientCredentialsTokenRetriever(
    IClientCredentialsTokenManagementService accessTokenManagementService,
    string tokenClientName
) : ITokenRetriever
{
    /// <inheritdoc />
    public async Task<ClientCredentialsToken> GetToken(HttpRequestMessage request, bool forceTokenRefresh, CancellationToken cancellationToken)
    {
        var parameters = new TokenRequestParameters
        {
            ForceRenewal = forceTokenRefresh
        };
        var token = await accessTokenManagementService.GetAccessTokenAsync(tokenClientName, parameters, cancellationToken);

        return token;

    }

    /// <inheritdoc />
    public AccessTokenManagementMetrics.TokenRequestType TokenRequestType { get; } = AccessTokenManagementMetrics.TokenRequestType.ClientCredentials;
}
