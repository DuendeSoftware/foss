// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

public interface IClientCredentialsTokenManager
{
    Task<TokenResult<ClientCredentialsToken>> GetAccessTokenAsync(
        TokenClientName clientName,
        TokenRequestParameters? parameters = null,
        CT ct = default);

    Task DeleteAccessTokenAsync(TokenClientName clientName,
        TokenRequestParameters? parameters = null,
        CT ct = default);
}
