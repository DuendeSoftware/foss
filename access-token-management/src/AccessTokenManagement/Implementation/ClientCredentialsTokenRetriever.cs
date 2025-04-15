// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OTel;

namespace Duende.AccessTokenManagement.Implementation;


/// <summary>
/// TokenRetriever that implements the logic on behalf of <see cref="AccessTokenHandler{TTokenRetriever,TToken}"/>
/// to retrieve a ClientCredentials Token. 
/// </summary>
/// <param name="accessTokenManagementService">Service that actually retrieves client credential access tokens</param>
/// <param name="tokenClientName"></param>
internal class ClientCredentialsTokenRetriever(
    IClientCredentialsTokenManagementService accessTokenManagementService,
    string tokenClientName
) : ITokenRetriever<ClientCredentialsToken>
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
