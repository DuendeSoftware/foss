// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Context;

/// <summary>
/// Context for the SendingRequest event
/// </summary>
public class SendingRequestContext(
    HttpContext context,
    AuthenticationScheme scheme,
    OAuth2IntrospectionOptions options,
    TokenIntrospectionRequest tokenIntrospectionRequest)
    : BaseContext<OAuth2IntrospectionOptions>(context, scheme, options)
{
    /// <summary>
    /// The <see cref="TokenIntrospectionRequest"/> request
    /// </summary>
    public TokenIntrospectionRequest TokenIntrospectionRequest => tokenIntrospectionRequest;
}
