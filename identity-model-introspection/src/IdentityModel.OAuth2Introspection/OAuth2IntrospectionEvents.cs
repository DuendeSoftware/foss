// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AspNetCore.Authentication.OAuth2Introspection.Context;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection;

/// <summary>
/// Default implementation.
/// </summary>
public class OAuth2IntrospectionEvents
{
    /// <summary>
    /// Invoked if exceptions are thrown during request processing. The exceptions will be re-thrown after this event unless suppressed.
    /// </summary>
    public Func<AuthenticationFailedContext, Task> OnAuthenticationFailed { get; set; } = context => Task.CompletedTask;

    /// <summary>
    /// Invoked after the security token has passed validation and a ClaimsIdentity has been generated.
    /// </summary>
    public Func<TokenValidatedContext, Task> OnTokenValidated { get; set; } = context => Task.CompletedTask;

    /// <summary>
    /// Invoked when client assertion need to be updated.
    /// </summary>
    public Func<UpdateClientAssertionContext, Task> OnUpdateClientAssertion { get; set; } = context => Task.CompletedTask;

    /// <summary>
    /// Invoked when sending token introspection request.
    /// </summary>
    public Func<SendingRequestContext, Task> OnSendingRequest { get; set; } = context => Task.CompletedTask;

    /// <summary>
    /// Invoked if exceptions are thrown during request processing. The exceptions will be re-thrown after this event unless suppressed.
    /// </summary>
    public virtual Task AuthenticationFailed(AuthenticationFailedContext context) => OnAuthenticationFailed(context);

    /// <summary>
    /// Invoked after the security token has passed validation and a ClaimsIdentity has been generated.
    /// </summary>
    public virtual Task TokenValidated(TokenValidatedContext context) => OnTokenValidated(context);

    /// <summary>
    /// Invoked when client assertion need to be updated.
    /// </summary>
    public virtual Task UpdateClientAssertion(UpdateClientAssertionContext context) => OnUpdateClientAssertion(context);

    /// <summary>
    /// Invoked when sending token introspection request.
    /// </summary>
    public virtual Task SendingRequest(SendingRequestContext context) => OnSendingRequest(context);
}
