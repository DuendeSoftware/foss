// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

/// <summary>
/// Keys that are used to inject different implementations into a specific service. 
/// </summary>
public static class ServiceProviderKeys
{
    /// <summary>
    /// Key for the client credentials token cache. Use this to inject a different cache implementation into the client credentials token cache.
    /// </summary>
    public const string ClientCredentialsTokenCache = "ClientCredentialsTokenCache";

    /// <summary>
    /// Key for the DPoP nonce store. Use this to inject a different cache into the DPoP nonce store.
    /// </summary>
    public const string DPoPNonceStore = "DPoPNonceStore";
}
