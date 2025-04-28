// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.Internal;

/// <summary>
/// The logic to generate a cache key. Defaults to a key based on <see cref="ClientCredentialsTokenManagementOptions.CacheKeyPrefix"/>, client name, scope, and resource.
/// Customize this if you add additional <see cref="TokenRequestParameters.Parameters"/> to your TokenRequest that
/// impact how this should be cached. 
/// </summary>
/// <param name="options"></param>
internal class DefaultClientCredentialsCacheKeyGenerator(IOptions<ClientCredentialsTokenManagementOptions> options) : IClientCredentialsCacheKeyGenerator
{
    public string GenerateKey(string clientName, TokenRequestParameters? parameters = null)
    {
        var scopePart = "s_" + parameters?.Scope;
        var resourcePart = "r_" + parameters?.Resource;

        return options.Value.CacheKeyPrefix + clientName + "::" + scopePart + "::" + resourcePart;
    }
}
