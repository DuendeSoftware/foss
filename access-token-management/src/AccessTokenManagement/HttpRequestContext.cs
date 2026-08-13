// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

/// <summary>
/// Represents a slim version of an HTTP request
/// </summary>
public record struct HttpRequestContext
{
    /// <summary>
    /// The HTTP method (for example <c>GET</c> or <c>POST</c>).
    /// </summary>
    public required string Method { get; init; }

    /// <summary>
    /// The request URI.
    /// </summary>
    public required Uri? RequestUri { get; init; }

    /// <summary>
    /// The request headers.
    /// </summary>
    public required IEnumerable<KeyValuePair<string, IEnumerable<string>>> Headers { get; init; }
}
