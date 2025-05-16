// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

/// <summary>
/// Abstraction for token endpoint operations
/// </summary>
public interface IClientCredentialsTokenEndpoint
{
    /// <summary>
    /// Requests a client credentials access token.
    /// </summary>
    /// <param name="clientName"></param>
    /// <param name="parameters"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<TokenResult<ClientCredentialsToken>> RequestAccessTokenAsync(
        ClientName clientName,
        TokenRequestParameters? parameters = null,
        CT ct = default);
}
