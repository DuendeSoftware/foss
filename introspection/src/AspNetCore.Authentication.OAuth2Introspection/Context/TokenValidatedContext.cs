// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Context;

/// <summary>
/// Context for the TokenValidated event
/// </summary>
public class TokenValidatedContext(
    HttpContext context,
    AuthenticationScheme scheme,
    OAuth2IntrospectionOptions options,
    string securityToken)
    : ResultContext<OAuth2IntrospectionOptions>(context, scheme, options)
{
    /// <summary>
    /// The security token
    /// </summary>
    public string SecurityToken => securityToken;
}
