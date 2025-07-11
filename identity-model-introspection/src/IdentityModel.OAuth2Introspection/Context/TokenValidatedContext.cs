// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;


<<<<<<< TODO: Unmerged change from project 'IdentityModel.AspNetCore.OAuth2Introspection(net9.0)', Before:
namespace IdentityModel.AspNetCore.OAuth2Introspection
{
    /// <summary>
    /// Context for the TokenValidated event
    /// </summary>
    public class TokenValidatedContext : ResultContext<OAuth2IntrospectionOptions>
    {
        /// <summary>
        /// ctor
        /// </summary>
        public TokenValidatedContext(
            HttpContext context,
            AuthenticationScheme scheme,
            OAuth2IntrospectionOptions options)
            : base(context, scheme, options) { }

        /// <summary>
        /// The security token
        /// </summary>
        public string SecurityToken { get; set; }
    }
=======
namespace IdentityModel.AspNetCore.OAuth2Introspection;

/// <summary>
/// Context for the TokenValidated event
/// </summary>
public class TokenValidatedContext : ResultContext<OAuth2IntrospectionOptions>
{
    /// <summary>
    /// ctor
    /// </summary>
    public TokenValidatedContext(
        HttpContext context,
        AuthenticationScheme scheme,
        OAuth2IntrospectionOptions options)
        : base(context, scheme, options) { }

    /// <summary>
    /// The security token
    /// </summary>
    public string SecurityToken { get; set; }
>>>>>>> After
namespace IdentityModel.AspNetCore.OAuth2Introspection;

/// <summary>
/// Context for the TokenValidated event
/// </summary>
public class TokenValidatedContext : ResultContext<OAuth2IntrospectionOptions>
{
    /// <summary>
    /// ctor
    /// </summary>
    public TokenValidatedContext(
        HttpContext context,
        AuthenticationScheme scheme,
        OAuth2IntrospectionOptions options)
        : base(context, scheme, options) { }

    /// <summary>
    /// The security token
    /// </summary>
    public string SecurityToken { get; set; }
}
