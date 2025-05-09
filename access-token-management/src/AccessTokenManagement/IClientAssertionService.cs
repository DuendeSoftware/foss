// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.Types;
using Duende.IdentityModel.Client;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Service to create client assertions for back-channel clients
/// </summary>
public interface IClientAssertionService
{
    /// <summary>
    /// Creates a client assertion based on client or configuration scheme (if present)
    /// </summary>
    /// <param name="clientName"></param>
    /// <param name="parameters"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ClientAssertion?> GetClientAssertionAsync(
        ClientCredentialsClientName? clientName = null,
        TokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default);
}
