// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Creates DPoP proofs and related key material metadata.
/// </summary>
public interface IDPoPProofService
{
    /// <summary>
    /// Creates a signed <see cref="DPoPProof"/> from request parameters.
    /// </summary>
    /// <param name="request">The inputs for generating the proof JWT.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The proof token, or <see langword="null"/> when no proof should be sent.</returns>
    Task<DPoPProof?> CreateProofTokenAsync(DPoPProofRequest request,
        CT ct = default);

    /// <summary>
    /// Computes the thumbprint of a proof key.
    /// </summary>
    /// <param name="dpopProofKey">The proof key to hash.</param>
    /// <returns>The thumbprint value, or <see langword="null"/> if it cannot be computed.</returns>
    DPoPProofThumbprint? GetProofKeyThumbprint(DPoPProofKey dpopProofKey);
}
