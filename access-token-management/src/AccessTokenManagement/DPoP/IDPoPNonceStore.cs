// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Service to keep track of DPoP nonces
/// </summary>
public interface IDPoPNonceStore
{
    /// <summary>
    /// Gets the nonce 
    /// </summary>
    Task<DPoPNonce?> GetNonceAsync(DPoPNonceContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the nonce 
    /// </summary>
    Task StoreNonceAsync(DPoPNonceContext context, DPoPNonce nonce, CancellationToken cancellationToken = default);
}
