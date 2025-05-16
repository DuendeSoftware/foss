// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Captures the information needed to request a dpop proof
/// </summary>
public sealed record DPopProofRequestParameters
{
    /// <summary>
    /// Existing http request message
    /// </summary>
    public required HttpRequestMessage Request { get; init; }

    /// <summary>
    /// Already retrieved client credentials token. 
    /// </summary>
    public required AccessTokenRequestHandler.IToken AccessToken { get; init; }

    /// <summary>
    /// Nonce (if present)
    /// </summary>
    public DPoPNonce? DPoPNonce { get; init; }
}
