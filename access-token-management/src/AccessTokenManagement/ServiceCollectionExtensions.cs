// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.DPoP.Internal;
using Duende.AccessTokenManagement.Internal;
using Duende.AccessTokenManagement.OTel;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Extension methods for IServiceCollection to register the client credentials token management services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all necessary services for client credentials token management
    /// </summary>
    /// <param name="services"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static ClientCredentialsTokenManagementBuilder AddClientCredentialsTokenManagement(
        this IServiceCollection services,
        Action<ClientCredentialsTokenManagementOptions> options)
    {
        services.Configure(options);
        return services.AddClientCredentialsTokenManagement();
    }

    /// <summary>
    /// Adds all necessary services for client credentials token management
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static ClientCredentialsTokenManagementBuilder AddClientCredentialsTokenManagement(
        this IServiceCollection services)
    {
        services.TryAddTransient<IClientCredentialsTokenManager, ClientCredentialsTokenManager>();
        services.TryAddSingleton<IClientCredentialsCacheDurationStore, ClientCredentialsCacheDurationStore>();
        services.AddHybridCache();

        // Add a default serializer for ClientCredentialsToken
        services
            .TryAddSingleton<IHybridCacheSerializer<ClientCredentialsToken>,
                AotTrimCompatibleClientCredentialsTokenSerializer>();

        // By default, resolve the default hybrid cache for the DefaultClientCredentialsTokenManager
        // without key. If desired, a consumers can register the distributed cache with a key
        services.TryAddKeyedSingleton<HybridCache>(ServiceProviderKeys.ClientCredentialsTokenCache,
            (sp, _) => sp.GetRequiredService<HybridCache>());
        services.TryAddTransient<IClientCredentialsTokenEndpoint, ClientCredentialsTokenClient>();
        services.TryAddTransient<IClientAssertionService, NoOpClientAssertionService>();

        services.TryAddTransient<IDPoPProofService, DefaultDPoPProofService>();
        services.TryAddTransient<IDPoPKeyStore, DefaultDPoPKeyStore>();

        // By default, resolve the default hybrid cache for the HybridDPoPNonceStore
        // without key. If desired, a consumers can register the distributed cache with a key
        services.TryAddKeyedSingleton<HybridCache>(ServiceProviderKeys.DPoPNonceStore,
            (sp, _) => sp.GetRequiredService<HybridCache>());
        services.AddTransient<IDPoPNonceStore, HybridDPoPNonceStore>();

        services.TryAddSingleton(TimeProvider.System);

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName);

        services.TryAddTransient<IClientCredentialsCacheKeyGenerator, DefaultClientCredentialsCacheKeyGenerator>();
        services.TryAddTransient<IDPoPNonceStoreKeyGenerator, DPoPNonceStoreKeyGenerator>();
        services.AddSingleton<AccessTokenManagementMetrics>();

        services.TryAddSingleton<IValidateOptions<ClientCredentialsClient>, ClientCredentialsClient.Validator>();

        return new ClientCredentialsTokenManagementBuilder(services);
    }

    /// <summary>
    /// Adds a named HTTP client for the factory that automatically sends a client access token
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="httpClientName">The name of the client.</param>
    /// <param name="clientName">The name of the token client.</param>
    /// <param name="configureClient">A delegate that is used to configure a <see cref="HttpClient"/>.</param>
    /// <returns></returns>
    public static IHttpClientBuilder AddClientCredentialsHttpClient(
        this IServiceCollection services,
        string httpClientName,
        ClientCredentialsClientName clientName,
        Action<HttpClient>? configureClient = null)
    {
        if (configureClient != null)
        {
            return services.AddHttpClient(httpClientName, configureClient)
                .AddDefaultAccessTokenResiliency()
                .AddClientCredentialsTokenHandler(clientName);
        }

        return services.AddHttpClient(httpClientName)
            .AddDefaultAccessTokenResiliency()
            .AddClientCredentialsTokenHandler(clientName);
    }

    /// <summary>
    /// Adds a named HTTP client for the factory that automatically sends a client access token
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="httpClientName">The name of the client.</param>
    /// <param name="clientName">The name of the token client.</param>
    /// <param name="configureClient">Additional configuration with service provider instance.</param>
    /// <returns></returns>
    public static IHttpClientBuilder AddClientCredentialsHttpClient(
        this IServiceCollection services,
        string httpClientName,
        ClientCredentialsClientName clientName,
        Action<IServiceProvider, HttpClient> configureClient) =>
        services.AddHttpClient(httpClientName, configureClient)
            .AddDefaultAccessTokenResiliency()
            .AddClientCredentialsTokenHandler(clientName);

    public static IHttpClientBuilder AddDefaultAccessTokenResiliency(this IHttpClientBuilder httpClientBuilder)
    {
        httpClientBuilder.AddResilienceHandler("Duende",
            (builder, context) => builder.AddDefaultAccessTokenHandlingResiliency(context));

        return httpClientBuilder;
    }

    /// <summary>
    /// Adds the client access token handler to an HttpClient
    /// </summary>
    /// <param name="httpClientBuilder"></param>
    /// <param name="clientName"></param>
    /// <returns></returns>
    public static IHttpClientBuilder AddClientCredentialsTokenHandler(
        this IHttpClientBuilder httpClientBuilder,
        ClientCredentialsClientName clientName) =>
        AddClientCredentialsTokenHandlerInternal(httpClientBuilder, tokenRequestCustomizerFactory: null, clientName);

    /// <summary>
    /// Adds the client access token handler to an HttpClient
    /// </summary>
    /// <param name="httpClientBuilder"></param>
    /// <param name="tokenRequestCustomizer"></param>
    /// <param name="clientName"></param>
    /// <returns></returns>
    public static IHttpClientBuilder AddClientCredentialsTokenHandler(
        this IHttpClientBuilder httpClientBuilder,
        ITokenRequestCustomizer tokenRequestCustomizer,
        ClientCredentialsClientName clientName)
    {
        ArgumentNullException.ThrowIfNull(tokenRequestCustomizer);
        return AddClientCredentialsTokenHandlerInternal(httpClientBuilder, _ => tokenRequestCustomizer, clientName);
    }

    /// <summary>
    /// Adds the client access token handler to an HttpClient
    /// </summary>
    /// <param name="httpClientBuilder"></param>
    /// <param name="tokenRequestCustomizerFactory"></param>
    /// <param name="clientName"></param>
    /// <returns></returns>
    public static IHttpClientBuilder AddClientCredentialsTokenHandler(
        this IHttpClientBuilder httpClientBuilder,
        Func<IServiceProvider, ITokenRequestCustomizer> tokenRequestCustomizerFactory,
        ClientCredentialsClientName clientName)
    {
        ArgumentNullException.ThrowIfNull(tokenRequestCustomizerFactory);
        return AddClientCredentialsTokenHandlerInternal(httpClientBuilder, tokenRequestCustomizerFactory, clientName);
    }

    private static IHttpClientBuilder AddClientCredentialsTokenHandlerInternal(
        IHttpClientBuilder httpClientBuilder,
        Func<IServiceProvider, ITokenRequestCustomizer>? tokenRequestCustomizerFactory,
        ClientCredentialsClientName clientName) => httpClientBuilder
        .AddHttpMessageHandler(provider =>
        {
            var accessTokenManagementService = provider.GetRequiredService<IClientCredentialsTokenManager>();
            var tokenRequestCustomizer = tokenRequestCustomizerFactory?.Invoke(provider) ??
                                         provider.GetService<ITokenRequestCustomizer>();
            var retriever =
                new ClientCredentialsTokenRetriever(accessTokenManagementService, clientName,
                    tokenRequestCustomizer);
            var accessTokenHandler = provider.BuildAccessTokenRequestHandler(retriever);

            return accessTokenHandler;
        });

    internal static AccessTokenRequestHandler BuildAccessTokenRequestHandler(
        this IServiceProvider provider,
        AccessTokenRequestHandler.ITokenRetriever retriever)
    {
        var logger = provider.GetRequiredService<ILogger<AccessTokenRequestHandler>>();
        var dPoPProofService = provider.GetRequiredService<IDPoPProofService>();
        var dPoPNonceStore = provider.GetRequiredService<IDPoPNonceStore>();
        var accessTokenHandler = new AccessTokenRequestHandler(
            tokenRetriever: retriever,
            dPoPNonceStore: dPoPNonceStore,
            dPoPProofService: dPoPProofService,
            logger: logger);

        return accessTokenHandler;
    }
}
