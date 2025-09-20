// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection;

/// <summary>
/// Extensions for registering the OAuth 2.0 introspection authentication handler
/// </summary>
public static class OAuth2IntrospectionExtensions
{
    /// <summary>
    /// Adds the OAuth 2.0 introspection handler.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static AuthenticationBuilder AddOAuth2Introspection(this AuthenticationBuilder builder)
        => builder.AddOAuth2Introspection(OAuth2IntrospectionDefaults.AuthenticationScheme);

    /// <summary>
    /// Adds the OAuth 2.0 introspection handler.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="authenticationScheme">The authentication scheme.</param>
    /// <returns></returns>
    public static AuthenticationBuilder AddOAuth2Introspection(this AuthenticationBuilder builder, string authenticationScheme)
        => builder.AddOAuth2Introspection(authenticationScheme, configureOptions: null);

    /// <summary>
    /// Adds the OAuth 2.0 introspection handler.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="configureOptions">The configure options.</param>
    /// <returns></returns>
    public static AuthenticationBuilder AddOAuth2Introspection(
        this AuthenticationBuilder services,
        Action<OAuth2IntrospectionOptions>? configureOptions)
        => services.AddOAuth2Introspection(OAuth2IntrospectionDefaults.AuthenticationScheme, configureOptions: configureOptions);

    /// <summary>
    /// Adds the OAuth 2.0 introspection handler.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="authenticationScheme">The authentication scheme.</param>
    /// <param name="configureOptions">The configure options.</param>
    /// <returns></returns>
    public static AuthenticationBuilder AddOAuth2Introspection(
        this AuthenticationBuilder builder,
        string authenticationScheme,
        Action<OAuth2IntrospectionOptions>? configureOptions)
    {
        builder.Services.AddHttpClient(OAuth2IntrospectionDefaults.BackChannelHttpClientName);
        var serviceDescriptor = ServiceDescriptor
            .Singleton<IPostConfigureOptions<OAuth2IntrospectionOptions>, PostConfigureOAuth2IntrospectionOptions>();
        builder.Services.TryAddEnumerable(serviceDescriptor);
        builder.AddScheme<OAuth2IntrospectionOptions, OAuth2IntrospectionHandler>(authenticationScheme, configureOptions);
        return builder;
    }
}
