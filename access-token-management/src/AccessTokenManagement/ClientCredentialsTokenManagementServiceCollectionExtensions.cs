// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.DPoP.Internal;
using Duende.AccessTokenManagement.Internal;
using Duende.AccessTokenManagement.OTel;
using Duende.AccessTokenManagement.Types;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Extension methods for IServiceCollection to register the client credentials token management services
/// </summary>
public static class ClientCredentialsTokenManagementServiceCollectionExtensions
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
    public static ClientCredentialsTokenManagementBuilder AddClientCredentialsTokenManagement(this IServiceCollection services)
    {

        services.TryAddTransient<IClientCredentialsTokenManagementService, ClientCredentialsTokenManager>();
        services.AddHybridCache();

        // By default, resolve the default hybridcache for the DefaultClientCredentialsTokenManager
        // without key. If desired, a consumers can register the distributed cache with a key
        services.TryAddKeyedSingleton<HybridCache>(ServiceProviderKeys.ClientCredentialsTokenCache, (sp, _) => sp.GetRequiredService<HybridCache>());
        services.TryAddTransient<IClientCredentialsTokenEndpointService, ClientCredentialsTokenEndpointService>();
        services.TryAddTransient<IClientAssertionService, NoOpClientAssertionService>();

        services.TryAddTransient<IDPopProofRequestHandler, DPopProofRequestHandler>();
        services.TryAddTransient<IDPoPProofService, DefaultDPoPProofService>();
        services.TryAddTransient<IDPoPKeyStore, DefaultDPoPKeyStore>();

        // By default, resolve the default hybridcache for the HybridDPoPNonceStore
        // without key. If desired, a consumers can register the distributed cache with a key
        services.TryAddKeyedSingleton<HybridCache>(ServiceProviderKeys.DPoPNonceStore, (sp, _) => sp.GetRequiredService<HybridCache>());
        services.AddTransient<IDPoPNonceStore, HybridDPoPNonceStore>();

        services.TryAddSingleton(TimeProvider.System);

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName);

        services.TryAddTransient<IClientCredentialsCacheKeyGenerator, DefaultClientCredentialsCacheKeyGenerator>();
        services.TryAddTransient<IDPoPNonceStoreKeyGenerator, DPoPNonceStoreKeyGenerator>();
        services.AddSingleton<AccessTokenManagementMetrics>();

        return new ClientCredentialsTokenManagementBuilder(services);
    }

    /// <summary>
    /// Adds a named HTTP client for the factory that automatically sends a client access token
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="httpClientName">The name of the client.</param>
    /// <param name="tokenClientName">The name of the token client.</param>
    /// <param name="configureClient">A delegate that is used to configure a <see cref="HttpClient"/>.</param>
    /// <returns></returns>
    public static IHttpClientBuilder AddClientCredentialsHttpClient(
    this IServiceCollection services,
    string httpClientName,
    ClientCredentialsClientName tokenClientName,
    Action<HttpClient>? configureClient = null)
    {
        if (configureClient != null)
        {
            return services.AddHttpClient(httpClientName, configureClient)
                .AddDefaultAccessTokenResiliency()
                .AddClientCredentialsTokenHandler(tokenClientName);
        }

        return services.AddHttpClient(httpClientName)
            .AddDefaultAccessTokenResiliency()
            .AddClientCredentialsTokenHandler(tokenClientName);
    }

    /// <summary>
    /// Adds a named HTTP client for the factory that automatically sends a client access token
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="httpClientName">The name of the client.</param>
    /// <param name="tokenClientName">The name of the token client.</param>
    /// <param name="configureClient">Additional configuration with service provider instance.</param>
    /// <returns></returns>
    public static IHttpClientBuilder AddClientCredentialsHttpClient(
        this IServiceCollection services,
        string httpClientName,
        ClientCredentialsClientName tokenClientName,
        Action<IServiceProvider, HttpClient> configureClient) =>
            services.AddHttpClient(httpClientName, configureClient)
                .AddDefaultAccessTokenResiliency()
                .AddClientCredentialsTokenHandler(tokenClientName);

    public static IHttpClientBuilder AddDefaultAccessTokenResiliency(this IHttpClientBuilder httpClientBuilder)
    {
        httpClientBuilder.AddResilienceHandler("Duende", builder => builder.AddDefaultAccessTokenHandlingResiliency());

        return httpClientBuilder;
    }

    /// <summary>
    /// Adds the client access token handler to an HttpClient
    /// </summary>
    /// <param name="httpClientBuilder"></param>
    /// <param name="tokenClientName"></param>
    /// <returns></returns>
    public static IHttpClientBuilder AddClientCredentialsTokenHandler(
        this IHttpClientBuilder httpClientBuilder,
        ClientCredentialsClientName tokenClientName) => httpClientBuilder
            .AddHttpMessageHandler(provider =>
                 {
                     var accessTokenManagementService = provider.GetRequiredService<IClientCredentialsTokenManagementService>();

                     var logger = provider.GetRequiredService<ILogger<AccessTokenRequestHandler>>();

                     var retriever = new ClientCredentialsTokenRetriever(accessTokenManagementService, tokenClientName);
                     var dpopHandler = provider.GetRequiredService<IDPopProofRequestHandler>();

                     var accessTokenHandler = new AccessTokenRequestHandler(
                         tokenRetriever: retriever,
                         dPoPProofRequestHandler: dpopHandler,
                         logger: logger);

                     return accessTokenHandler;

                 });
}
