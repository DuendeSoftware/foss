// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;


<<<<<<< TODO: Unmerged change from project 'IdentityModel.AspNetCore.OAuth2Introspection(net9.0)', Before:
namespace IdentityModel.AspNetCore.OAuth2Introspection
{
    /// <summary>
    /// Context for the AuthenticationFailed event
    /// </summary>
    public class AuthenticationFailedContext : ResultContext<OAuth2IntrospectionOptions>
    {
        /// <summary>
        /// ctor
        /// </summary>
        public AuthenticationFailedContext(
            HttpContext context,
            AuthenticationScheme scheme,
            OAuth2IntrospectionOptions options)
            : base(context, scheme, options) { }

        /// <summary>
        /// The error
        /// </summary>
        public string Error { get; set; }
    }
=======
namespace IdentityModel.AspNetCore.OAuth2Introspection;

/// <summary>
/// Context for the AuthenticationFailed event
/// </summary>
public class AuthenticationFailedContext : ResultContext<OAuth2IntrospectionOptions>
{
    /// <summary>
    /// ctor
    /// </summary>
    public AuthenticationFailedContext(
        HttpContext context,
        AuthenticationScheme scheme,
        OAuth2IntrospectionOptions options)
        : base(context, scheme, options) { }

    /// <summary>
    /// The error
    /// </summary>
    public string Error { get; set; }
>>>>>>> After
namespace IdentityModel.AspNetCore.OAuth2Introspection;

/// <summary>
/// Context for the AuthenticationFailed event
/// </summary>
public class AuthenticationFailedContext : ResultContext<OAuth2IntrospectionOptions>
{
    /// <summary>
    /// ctor
    /// </summary>
    public AuthenticationFailedContext(
        HttpContext context,
        AuthenticationScheme scheme,
        OAuth2IntrospectionOptions options)
        : base(context, scheme, options) { }

    /// <summary>
    /// The error
    /// </summary>
    public string Error { get; set; }
}
