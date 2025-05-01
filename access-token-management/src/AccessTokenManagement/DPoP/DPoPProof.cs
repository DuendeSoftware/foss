// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Models a DPoP proof token
/// </summary>
public record DPoPProof
{
    /// <summary>
    /// The proof token
    /// </summary>
    public required DPoPProofToken ProofToken { get; init; }
}
