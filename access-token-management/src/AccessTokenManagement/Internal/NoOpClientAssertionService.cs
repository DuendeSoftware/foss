// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;

namespace Duende.AccessTokenManagement.Internal;

/// <summary>
/// By default, we don't do client assertions. 
/// </summary>
internal class NoOpClientAssertionService : IClientAssertionService
{
    /// <inheritdoc />
    public Task<ClientAssertion?> GetClientAssertionAsync(ClientName? clientName = null,
        TokenRequestParameters? parameters = null,
        CT ct = default) =>
        Task.FromResult<ClientAssertion?>(null);
}
