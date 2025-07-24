// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using System.Threading;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Implements token management logic
/// </summary>
public class ClientCredentialsTokenManagementService(
    IClientCredentialsTokenEndpointService clientCredentialsTokenEndpointService,
    IClientCredentialsTokenCache tokenCache,
    ILogger<ClientCredentialsTokenManagementService> logger
) : IClientCredentialsTokenManagementService
{

    /// <inheritdoc/>
    public async Task<ClientCredentialsToken> GetAccessTokenAsync(
        string clientName,
        TokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        parameters ??= new TokenRequestParameters();

        if (parameters.ForceRenewal == false)
        {
            try
            {
                var item = await tokenCache.GetAsync(
                    clientName: clientName, 
                    requestParameters: parameters, 
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                if (item != null)
                {
                    return item;
                }
            }
            catch (Exception e)
            {
                logger.LogError(e,
                    "Error trying to obtain token from cache for client {clientName}. Error = {error}. Will obtain new token.", 
                    clientName, e.Message);
            }
        }

        return await tokenCache.GetOrCreateAsync(
            clientName: clientName, 
            requestParameters: parameters, 
            factory: InvokeGetAccessToken, 
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<ClientCredentialsToken> InvokeGetAccessToken(string clientName, TokenRequestParameters parameters, CancellationToken cancellationToken)
    {
        return await clientCredentialsTokenEndpointService.RequestToken(clientName, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task DeleteAccessTokenAsync(
        string clientName,
        TokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        parameters ??= new TokenRequestParameters();
        return tokenCache.DeleteAsync(clientName, parameters, cancellationToken);
    }
}