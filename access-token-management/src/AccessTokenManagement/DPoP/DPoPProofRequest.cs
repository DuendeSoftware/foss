// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Parameters required to create a <see cref="DPoPProof"/> for an outgoing HTTP request.
/// </summary>
public sealed record DPoPProofRequest
{
    /// <summary>
    /// The HTTP URL of the request
    /// </summary>
    public required Uri Url { get; init; }

    /// <summary>
    /// The HTTP method of the request
    /// </summary>
    public required HttpMethod Method { get; init; }

    /// <summary>
    /// The proof key used to sign the DPoP proof.
    /// </summary>
    public required DPoPProofKey DPoPProofKey { get; init; }

    /// <summary>
    /// The server-provided nonce to include in the generated proof, when required.
    /// </summary>
    public DPoPNonce? DPoPNonce { get; init; }

    /// <summary>
    /// The access token that the proof is bound to, when available.
    /// </summary>
    public AccessToken? AccessToken { get; init; }

    /// <summary>
    /// Additional claims to add to the DPoP proof payload.
    /// </summary>
    public IReadOnlyDictionary<string, string>? AdditionalPayloadClaims { get; init; }
}
