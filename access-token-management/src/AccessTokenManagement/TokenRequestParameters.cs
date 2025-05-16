// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Additional optional parameters for a client credentials access token request
/// </summary>
public record TokenRequestParameters
{
    /// <summary>
    /// Force renewal of token.
    /// </summary>
    public ForceTokenRenewal ForceTokenRenewal { get; init; } = new(false);

    /// <summary>
    /// Override the statically configured scope parameter.
    /// </summary>
    public Scope? Scope { get; init; }

    /// <summary>
    /// Override the statically configured resource parameter.
    /// </summary>
    public Resource? Resource { get; init; }

    /// <summary>
    /// Additional parameters to send.
    /// </summary>
    public Parameters Parameters { get; init; } = [];

    /// <summary>
    /// Specifies the client assertion.
    /// </summary>
    public ClientAssertion? Assertion { get; init; }

    /// <summary>
    /// Additional context that might be relevant in the pipeline
    /// </summary>
    public Parameters Context { get; init; } = [];
}
