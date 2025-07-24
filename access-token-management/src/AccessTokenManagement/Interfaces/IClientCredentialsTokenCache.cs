// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

/// <summary>
/// Abstraction for caching client credentials access tokens
/// </summary>
public interface IClientCredentialsTokenCache
{
    /// <summary>
    /// Caches a client access token
    /// </summary>
    /// <param name="clientName"></param>
    /// <param name="clientCredentialsToken"></param>
    /// <param name="requestParameters"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SetAsync(
        string clientName,
        ClientCredentialsToken clientCredentialsToken,
        TokenRequestParameters requestParameters,
        CancellationToken cancellationToken = default);

    Task<ClientCredentialsToken> GetOrCreateAsync(
        string clientName,
        TokenRequestParameters requestParameters,
        Func<string, TokenRequestParameters, CancellationToken, Task<ClientCredentialsToken>> factory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a client access token from the cache
    /// </summary>
    /// <param name="clientName"></param>
    /// <param name="requestParameters"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ClientCredentialsToken?> GetAsync(
        string clientName,
        TokenRequestParameters requestParameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a client access token from the cache
    /// </summary>
    /// <param name="clientName"></param>
    /// <param name="requestParameters"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task DeleteAsync(
        string clientName,
        TokenRequestParameters requestParameters,
        CancellationToken cancellationToken = default);
}