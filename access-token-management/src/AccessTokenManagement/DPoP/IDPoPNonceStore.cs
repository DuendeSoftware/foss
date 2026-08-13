// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Stores and retrieves server-provided DPoP nonces between requests.
/// </summary>
public interface IDPoPNonceStore
{
    /// <summary>
    /// Gets the nonce for a DPoP request context.
    /// </summary>
    /// <param name="context">The context used to locate the stored nonce.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The nonce, or <see langword="null"/> when none is stored.</returns>
    Task<DPoPNonce?> GetNonceAsync(DPoPNonceContext context, CT ct = default);

    /// <summary>
    /// Stores a nonce for a DPoP request context.
    /// </summary>
    /// <param name="context">The context used to key the nonce value.</param>
    /// <param name="nonce">The nonce to store.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when persistence finishes.</returns>
    Task StoreNonceAsync(DPoPNonceContext context, DPoPNonce nonce, CT ct = default);
}
