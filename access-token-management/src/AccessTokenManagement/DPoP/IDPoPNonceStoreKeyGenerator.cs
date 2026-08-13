// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Generates stable cache keys for <see cref="IDPoPNonceStore"/> entries.
/// </summary>
public interface IDPoPNonceStoreKeyGenerator
{
    /// <summary>
    /// Generates a cache key for a DPoP nonce context.
    /// </summary>
    /// <param name="context">The request context used to derive the key.</param>
    /// <returns>A cache key for the nonce store.</returns>
    string GenerateKey(DPoPNonceContext context);
}
