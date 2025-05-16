// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Models a DPoP proof token.
/// </summary>
public sealed record DPoPProof
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
    /// The JSON web key used to sign the DPoP proof.
    /// </summary>
    public required ProofKeyString ProofKey { get; init; }

    /// <summary>
    /// The nonce value for the DPoP proof token.
    /// </summary>
    public DPoPNonce? DPoPNonce { get; init; }

    /// <summary>
    /// The access token
    /// </summary>
    public AccessTokenString? AccessToken { get; init; }

    /// <summary>
    /// Additional claims to add to the DPoP proof payload.
    /// </summary>
    public IReadOnlyDictionary<string, string>? AdditionalPayloadClaims { get; init; }
}
