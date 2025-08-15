// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;

namespace Duende.AccessTokenManagement.Framework;

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

        Nonce = request.DPoPNonce?.ToString();
        return Task.FromResult<DPoPProof?>(DPoPProof.Parse(ProofToken + Nonce));
    }

    public DPoPProofThumbprint? GetProofKeyThumbprint(DPoPProofKey request) => null;
}
