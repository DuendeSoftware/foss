// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Service to access DPoP keys
/// </summary>
public interface IDPoPKeyStore
{
    /// <summary>
    /// Gets the DPoP key for the client, or null if none available for the client
    /// </summary>
    Task<ProofKeyString?> GetKeyAsync(ClientName clientName,
        CT ct = default);
}
