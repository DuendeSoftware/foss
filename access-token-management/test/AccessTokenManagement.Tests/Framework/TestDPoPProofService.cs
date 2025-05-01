// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.Types;

namespace Duende.AccessTokenManagement.Tests;

public class TestDPoPProofService : IDPoPProofService
{
    public string? ProofToken { get; set; }
    public string? Nonce { get; set; }
    public bool AppendNonce { get; set; }

    public Task<DPoPProof?> CreateProofTokenAsync(DPoPProofRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ProofToken == null)
        {
            return Task.FromResult<DPoPProof?>(null);
        }

        Nonce = request.DPoPNonce?.Value;
        return Task.FromResult<DPoPProof?>(new DPoPProof { ProofToken = DPoPProofToken.Parse(ProofToken + Nonce) });
    }

    public DPoPProofThumbPrint? GetProofKeyThumbprint(DPoPJsonWebKey request) => null;
}
