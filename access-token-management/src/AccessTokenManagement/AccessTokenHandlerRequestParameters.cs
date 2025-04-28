// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

/// <summary>
/// Captures information about requests being sent via access token handler,
/// so that the retry policy and the access token handler can work together 
/// </summary>
public record AccessTokenHandlerRequestParameters
{
    public required HttpRequestMessage Request { get; init; }
    public string? DPoPNonce { get; init; }
    public bool ForceTokenRefresh { get; init; }
}
