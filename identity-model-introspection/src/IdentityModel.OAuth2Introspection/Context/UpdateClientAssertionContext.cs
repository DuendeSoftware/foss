// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Context;

/// <summary>
/// Context for the UpdateClientAssertion event
/// </summary>
public class UpdateClientAssertionContext : ResultContext<OAuth2IntrospectionOptions>
{
    /// <summary>
    /// ctor
    /// </summary>
    public UpdateClientAssertionContext(
        HttpContext context,
        AuthenticationScheme scheme,
        OAuth2IntrospectionOptions options)
        : base(context, scheme, options) { }

    /// <summary>
    /// The client assertion
    /// </summary>
    public ClientAssertion ClientAssertion { get; set; }

    /// <summary>
    /// The client assertion expiration time
    /// </summary>
    public DateTime ClientAssertionExpirationTime { get; set; }
}
