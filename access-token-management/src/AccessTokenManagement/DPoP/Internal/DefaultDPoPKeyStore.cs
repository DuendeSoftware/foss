// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.DPoP.Internal;

/// <summary>
/// Default implementation, which reads the dpop key from the client configuration.
/// </summary>
internal class DefaultDPoPKeyStore(IOptionsMonitor<ClientCredentialsClient> options) : IDPoPKeyStore
{
    /// <inheritdoc/>
    public virtual Task<ProofKeyString?> GetKeyAsync(ClientName clientName,
        CT ct = default)
    {
        var client = options.Get(clientName.ToString());


        if (client.DPoPJsonWebKey == null)
        {
            return Task.FromResult<ProofKeyString?>(null);
        }

        return Task.FromResult(client.DPoPJsonWebKey);
    }
}
