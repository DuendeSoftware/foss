// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OpenIdConnect.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Extension methods for IServiceCollection to register the user token management services
/// </summary>
public static class OpenIdConnectTokenManagementServiceCollectionExtensions
{
    /// <summary>
    /// Adds the necessary services to manage user tokens based on OpenID Connect configuration
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddOpenIdConnectAccessTokenManagement(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.AddClientCredentialsTokenManagement();
        services.AddSingleton<IConfigureOptions<ClientCredentialsClient>, ConfigureOpenIdConnectClientCredentialsOptions>();

        // TODO: maybe return a builder with a ConfigureScheme that adds IConfigureNamedOptions/IPostConfigureNamedOptions with the naming convention?
        // for example, per-scheme client credentials style, scope, etc settings
#pragma warning disable CS0618 // Type or member is obsolete
        services.TryAddTransient<IUserTokenManagementService, UserAccessAccessTokenManagementService>();
        services.TryAddTransient<IOpenIdConnectConfigurationService, OpenIdConnectConfigurationService>();
        services.TryAddSingleton<IUserTokenRequestSynchronization, UserTokenRequestSynchronization>();
        services.TryAddTransient<IUserTokenEndpointService, UserTokenEndpointService>();
        services.TryAddSingleton<IStoreTokensInAuthenticationProperties, StoreTokensInAuthenticationProperties>();
#pragma warning restore CS0618 // Type or member is obsolete
        services.ConfigureOptions<ConfigureOpenIdConnectOptions>();

        // By default, we assume that we are in a traditional web application
        // where we can use the http context. The services below depend on http
        // context, and we register different ones in blazor

#pragma warning disable CS0618 // Type or member is obsolete
        services.TryAddScoped<IUserAccessor, HttpContextUserAccessor>();
        services.TryAddScoped<IUserTokenStore, AuthenticationSessionUserAccessTokenStore>();
#pragma warning restore CS0618 // Type or member is obsolete

        // scoped since it will be caching per-request authentication results
        services.AddScoped<AuthenticateResultCache>();

        return services;
    }

    /// <summary>
    /// Adds implementations of services that enable access token management in
    /// Blazor Server.
    /// </summary>
    /// <typeparam name="TTokenStore">An IUserTokenStore implementation. Blazor
    /// Server requires an IUserTokenStore because the default token store
    /// relies on cookies, which are not present when streaming updates over a
    /// blazor circuit. </typeparam>
    public static IServiceCollection AddBlazorServerAccessTokenManagement<TTokenStore>(this IServiceCollection services)
        where TTokenStore : class, IUserTokenStore
    {
        services.AddScoped<IUserTokenStore, TTokenStore>();
#pragma warning disable CS0618 // Type or member is obsolete
        services.AddScoped<IUserAccessor, BlazorServerUserAccessor>();
#pragma warning restore CS0618 // Type or member is obsolete
        services.AddCircuitServicesAccessor();
        services.AddHttpContextAccessor(); // For SSR

        return services;
    }

    /// <summary>
    /// Adds the necessary services to manage user tokens based on OpenID Connect configuration
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configureAction"></param>
    /// <returns></returns>
    public static IServiceCollection AddOpenIdConnectAccessTokenManagement(this IServiceCollection services,
        Action<UserTokenManagementOptions> configureAction)
    {
        services.Configure(configureAction);

        return services.AddOpenIdConnectAccessTokenManagement();
    }
    /// <summary>
    /// Adds a typed HTTP client for the factory that automatically sends the current user access token
    /// </summary>
    /// <typeparam name="T">The typed http client</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="parameters"></param>
    /// <param name="configureClient">Additional configuration with service provider instance.</param>
    /// <returns></returns>
    public static IHttpClientBuilder AddUserAccessTokenHttpClient<T>(this IServiceCollection services,
        UserTokenRequestParameters? parameters = null,
        Action<IServiceProvider, HttpClient>? configureClient = null) where T : class
    {
        if (configureClient != null)
        {
            return services.AddHttpClient<T>(configureClient)
                .AddDefaultAccessTokenResiliency()
                .AddUserAccessTokenHandler(parameters);
        }

        return services.AddHttpClient<T>()
            .AddDefaultAccessTokenResiliency()
            .AddUserAccessTokenHandler(parameters);
    }
    /// <summary>
    /// Adds a named HTTP client for the factory that automatically sends the current user access token
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="name">The name of the client.</param>
    /// <param name="parameters"></param>
    /// <param name="configureClient">Additional configuration with service provider instance.</param>
    /// <returns></returns>
    public static IHttpClientBuilder AddUserAccessTokenHttpClient(this IServiceCollection services,
        string name,
        UserTokenRequestParameters? parameters = null,
        Action<IServiceProvider, HttpClient>? configureClient = null)
    {
        if (configureClient != null)
        {
            return services.AddHttpClient(name, configureClient)
                .AddDefaultAccessTokenResiliency()
                .AddUserAccessTokenHandler(parameters);
        }

        return services.AddHttpClient(name)
            .AddDefaultAccessTokenResiliency()
            .AddUserAccessTokenHandler(parameters);
    }

    /// <summary>
    /// Adds a named HTTP client for the factory that automatically sends the current user access token
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="name">The name of the client.</param>
    /// <param name="parameters"></param>
    /// <param name="configureClient">Additional configuration with service provider instance.</param>
    /// <returns></returns>
    public static IHttpClientBuilder AddUserAccessTokenHttpClient(this IServiceCollection services,
        string name,
        UserTokenRequestParameters? parameters = null,
        Action<HttpClient>? configureClient = null)
    {
        if (configureClient != null)
        {
            return services.AddHttpClient(name, configureClient)
                .AddDefaultAccessTokenResiliency()
                .AddUserAccessTokenHandler(parameters);
        }

        return services.AddHttpClient(name)
            .AddDefaultAccessTokenResiliency()
            .AddUserAccessTokenHandler(parameters);
    }

    /// <summary>
    /// Adds a typed HTTP client for the factory that automatically sends the current client access token. The client access token is an access token that is not associated with any user, obtained with the client credentials flow.
    /// </summary>
    /// <typeparam name="T">The typed http client</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="parameters"></param>
    /// <param name="configureClient">Additional configuration with service provider instance.</param>
    /// <returns></returns>
    public static IHttpClientBuilder AddClientAccessTokenHttpClient<T>(this IServiceCollection services,
        UserTokenRequestParameters? parameters = null,
        Action<HttpClient>? configureClient = null) where T : class
    {
        if (configureClient != null)
        {
            return services.AddHttpClient<T>(configureClient)
                .AddDefaultAccessTokenResiliency()
                .AddClientAccessTokenHandler(parameters);
        }

        return services.AddHttpClient<T>()
            .AddDefaultAccessTokenResiliency()
            .AddClientAccessTokenHandler(parameters);
    }

    /// <summary>
    /// Adds a named HTTP client for the factory that automatically sends the current client access token. The client access token is an access token that is not associated with any user, obtained with the client credentials flow.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="name">The name of the client.</param>
    /// <param name="parameters"></param>
    /// <param name="configureClient">Additional configuration with service provider instance.</param>
    /// <returns></returns>
    public static IHttpClientBuilder AddClientAccessTokenHttpClient(this IServiceCollection services,
        string name,
        UserTokenRequestParameters? parameters = null,
        Action<HttpClient>? configureClient = null)
    {
        if (configureClient != null)
        {
            return services.AddHttpClient(name, configureClient)
                .AddDefaultAccessTokenResiliency()
                .AddClientAccessTokenHandler(parameters);
        }

        return services.AddHttpClient(name)
            .AddDefaultAccessTokenResiliency()
            .AddClientAccessTokenHandler(parameters);
    }


    /// <summary>
    /// Adds the user access token handler to an HttpClient
    /// </summary>
    /// <param name="httpClientBuilder"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public static IHttpClientBuilder AddUserAccessTokenHandler(
        this IHttpClientBuilder httpClientBuilder,
        UserTokenRequestParameters? parameters = null) =>
        httpClientBuilder.AddHttpMessageHandler(provider =>
        {
            var dPoPProofRequestHandler = provider.GetRequiredService<IDPopProofRequestHandler>();
            var logger = provider.GetRequiredService<ILogger<AccessTokenRequestHandler>>();
            var httpContextAccessor = provider.GetRequiredService<IUserAccessor>();
            var userTokenManagementService = provider.GetRequiredService<IUserTokenManagementService>();

            var tokenRetriever = new OpenIdConnectUserAccessTokenRetriever(
                httpContextAccessor,
                userTokenManagementService,
                parameters);

            return new AccessTokenRequestHandler(
                tokenRetriever,
                dPoPProofRequestHandler,
                logger);

#pragma warning restore CS0618 // Type or member is obsolete

        });

    /// <summary>
    /// Adds the client access token handler to an HttpClient
    /// </summary>
    /// <param name="httpClientBuilder"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public static IHttpClientBuilder AddClientAccessTokenHandler(
        this IHttpClientBuilder httpClientBuilder,
        UserTokenRequestParameters? parameters = null) =>
        httpClientBuilder.AddHttpMessageHandler(provider =>
        {
            var dPoPProofRequestHandler = provider.GetRequiredService<IDPopProofRequestHandler>();
            var logger = provider.GetRequiredService<ILogger<AccessTokenRequestHandler>>();
            var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();

            var tokenRetriever = new OpenIdConnectClientAccessTokenRetriever(httpContextAccessor,
                parameters);

            return new AccessTokenRequestHandler(
                tokenRetriever,
                dPoPProofRequestHandler,
                logger);

        });
#pragma warning restore CS0618 // Type or member is obsolete

}
