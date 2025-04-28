// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

/// <summary>
/// Captures the information needed to requrest a dpop proof
/// </summary>
public record DPopProofRequestParameters
{
    /// <summary>
    /// Exising http request message
    /// </summary>
    public required HttpRequestMessage Request { get; init; }

    /// <summary>
    /// Already retrieved client credentials token. 
    /// </summary>
    public required ClientCredentialsToken ClientCredentialsToken { get; init; }

    /// <summary>
    /// Nonce (if present)
    /// </summary>
    public string? DPoPNonce { get; init; }
}
