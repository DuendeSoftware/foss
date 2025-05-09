// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.Types;

namespace Duende.AccessTokenManagement;

public interface IClientCredentialsTokenManagementService
{
    Task<TokenResult<ClientCredentialsToken>> GetAccessTokenAsync(
        ClientCredentialsClientName clientName,
        TokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default);

    Task DeleteAccessTokenAsync(ClientCredentialsClientName clientName,
        TokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default);
}
