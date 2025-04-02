// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OTel;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Implements token management logic
/// </summary>
[Obsolete(Constants.AtmPublicSurfaceInternal, UrlFormat = Constants.AtmPublicSurfaceLink)]
public class ClientCredentialsTokenManagementService(
    AccessTokenManagementMetrics metrics,
    IClientCredentialsTokenEndpointService clientCredentialsTokenEndpointService,
    IClientCredentialsTokenCache tokenCache) : IClientCredentialsTokenManagementService
{
    /// <inheritdoc/>
    public async Task<ClientCredentialsToken> GetAccessTokenAsync(
        string clientName,
        TokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        parameters ??= new TokenRequestParameters();

        var token = await tokenCache.GetOrCreateAsync(
            clientName: clientName,
            requestParameters: parameters,
            factory: InvokeGetAccessToken,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        metrics.AccessTokenUsed(token.ClientId, AccessTokenManagementMetrics.TokenRequestType.ClientCredentials);

        return token;
    }

    private async Task<ClientCredentialsToken> InvokeGetAccessToken(string clientName, TokenRequestParameters parameters, CancellationToken cancellationToken)
    {
        return await clientCredentialsTokenEndpointService.RequestToken(clientName, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAccessTokenAsync(
        string clientName,
        TokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        parameters ??= new TokenRequestParameters();
        await tokenCache.DeleteAsync(clientName, parameters, cancellationToken);
    }
}
