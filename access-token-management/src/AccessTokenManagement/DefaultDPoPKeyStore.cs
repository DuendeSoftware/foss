// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Default implementation
/// </summary>
[Obsolete(Constants.AtmPublicSurfaceInternal, UrlFormat = Constants.AtmPublicSurfaceLink)]
public class DefaultDPoPKeyStore(IOptionsMonitor<ClientCredentialsClient> options) : IDPoPKeyStore
{
    /// <inheritdoc/>
    public virtual Task<DPoPKey?> GetKeyAsync(string clientName)
    {
        var client = options.Get(clientName);

        if (string.IsNullOrWhiteSpace(client.DPoPJsonWebKey))
        {
            return Task.FromResult<DPoPKey?>(null!);
        }

        return Task.FromResult<DPoPKey?>(new DPoPKey { JsonWebKey = client.DPoPJsonWebKey });
    }
}
