// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

/// <summary>
/// The logic to generate a key to store a DPoP nonce in the Cache
/// </summary>
public interface IDPoPNonceStoreKeyGenerator
{
    /// <summary>
    /// Method to generate a cache key for a DPoP nonce
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    string GenerateKey(DPoPNonceContext context);
}
