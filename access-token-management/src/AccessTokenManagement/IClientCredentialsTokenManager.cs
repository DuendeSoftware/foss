// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

/// <summary>
/// Provides the main API for acquiring and managing client-credentials access tokens.
/// </summary>
/// <remarks>
/// Implementations are responsible for caching, renewal, and token endpoint interaction
/// for configured <see cref="ClientCredentialsClientName"/> clients.
/// </remarks>
public interface IClientCredentialsTokenManager
{
    /// <summary>
    /// Gets an access token for the named client.
    /// </summary>
    /// <param name="clientName">The configured client-credentials client name.</param>
    /// <param name="parameters">Optional request parameters that influence token retrieval and caching.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="TokenResult{T}"/> containing either a token or protocol failure details.</returns>
    Task<TokenResult<ClientCredentialsToken>> GetAccessTokenAsync(
        ClientCredentialsClientName clientName,
        TokenRequestParameters? parameters = null,
        CT ct = default);

    /// <summary>
    /// Deletes the cached access token for the named client and request parameters.
    /// </summary>
    /// <param name="clientName">The configured client-credentials client name.</param>
    /// <param name="parameters">Optional request parameters used to identify the cached token entry.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when deletion has finished.</returns>
    Task DeleteAccessTokenAsync(ClientCredentialsClientName clientName,
        TokenRequestParameters? parameters = null,
        CT ct = default);
}
