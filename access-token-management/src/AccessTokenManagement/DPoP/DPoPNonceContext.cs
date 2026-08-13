// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Identifies the request target for storing and retrieving DPoP nonces.
/// </summary>
public sealed record DPoPNonceContext
{
    /// <summary>
    /// The HTTP URL of the request.
    /// </summary>
    public required Uri Url { get; set; }

    /// <summary>
    /// The HTTP method of the request.
    /// </summary>
    public required HttpMethod Method { get; set; }
}
