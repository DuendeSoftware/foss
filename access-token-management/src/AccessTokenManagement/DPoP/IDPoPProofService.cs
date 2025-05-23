// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Service to create DPoP proof tokens
/// </summary>
public interface IDPoPProofService
{
    /// <summary>
    /// Serializes a requested <see cref="DPoPProofRequest"/> model into a <see cref="DPoPProof"/>.
    /// </summary>
    Task<DPoPProof?> CreateProofTokenAsync(DPoPProofRequest request,
        CT ct = default);

    /// <summary>
    /// Computes the thumbprint of the JSON web key.
    /// </summary>
    DPoPProofThumbprint? GetProofKeyThumbprint(DPoPProofKey dpopProofKey);
}
