// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

/// <summary>
/// Service to access DPoP keys
/// </summary>
public interface IDPoPKeyStore
{
    /// <summary>
    /// Gets the DPoP key for the client, or null if none available for the client
    /// </summary>
    Task<DPoPKey?> GetKeyAsync(string clientName);
}

/// <summary>
/// Models a DPoP key
/// </summary>
public class DPoPKey
{
    /// <summary>
    /// The string representation of the JSON web key
    /// </summary>
    public required string JsonWebKey { get; set; }
}
