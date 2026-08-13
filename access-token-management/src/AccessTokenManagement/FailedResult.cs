// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

/// <summary>
/// Represents a protocol-level token acquisition failure.
/// </summary>
/// <param name="Error">The OAuth/OIDC error code.</param>
/// <param name="ErrorDescription">An optional error description from the token endpoint.</param>
public sealed record FailedResult(string Error, string? ErrorDescription = null) : TokenResult
{
    /// <summary>
    /// Formats the failure details for logging and diagnostics.
    /// </summary>
    public override string ToString()
    {
        var description = string.IsNullOrEmpty(ErrorDescription) ? string.Empty : $" with description {ErrorDescription}";
        return $"Failed to retrieve access token due to {Error}{description}.";
    }
}
