// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Default implementation, which reads the dpop key from the client configuration.
/// </summary>
internal class DefaultDPoPKeyStore(IOptionsMonitor<ClientCredentialsClient> options) : IDPoPKeyStore
{
    /// <inheritdoc/>
    public virtual Task<DPoPJsonWebKey?> GetKeyAsync(ClientName clientName,
        CancellationToken cancellationToken = default)
    {
        var client = options.Get(clientName.ToString());


        if (client.DPoPJsonWebKey == null)
        {
            return Task.FromResult<DPoPJsonWebKey?>(null);
        }

        return Task.FromResult<DPoPJsonWebKey?>(client.DPoPJsonWebKey);
    }
}
