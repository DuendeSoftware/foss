// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Context;

/// <summary>
/// Context for the UpdateClientAssertion event
/// </summary>
public class UpdateClientAssertionContext(
    HttpContext context,
    AuthenticationScheme scheme,
    OAuth2IntrospectionOptions options,
    ClientAssertion clientAssertion)
    : ResultContext<OAuth2IntrospectionOptions>(context, scheme, options)
{
    /// <summary>
    /// The client assertion
    /// </summary>
    public ClientAssertion ClientAssertion => clientAssertion;

    /// <summary>
    /// The client assertion expiration time
    /// </summary>
    public DateTime ClientAssertionExpirationTime => Options.ClientAssertionExpirationTime;
}
