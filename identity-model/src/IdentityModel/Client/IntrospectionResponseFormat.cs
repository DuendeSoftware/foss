// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.IdentityModel.Client;

/// <summary>
/// Specifies the format of the token introspection response.
/// </summary>
public enum IntrospectionResponseFormat
{
    /// <summary>
    /// Plain JSON introspection response (default).
    /// </summary>
    Json,

    /// <summary>
    /// JWT introspection response.
    /// </summary>
    Jwt
}
