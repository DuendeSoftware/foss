// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.Implementation;
using Duende.AccessTokenManagement.OTel;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        services.TryAddSingleton<ITokenRequestSynchronization, TokenRequestSynchronization>();

#pragma warning disable CS0618 // Type or member is obsolete
        services.TryAddTransient<IClientCredentialsTokenManagementService, ClientCredentialsTokenManagementService>();


        // By default, resolve the distributed cache for the DistributedClientCredentialsTokenCache
        // without key. If desired, a consumers can register the distributed cache with a key
        services.TryAddKeyedSingleton<IDistributedCache>(ServiceProviderKeys.ClientCredentialsTokenCache, (sp, _) => sp.GetRequiredService<IDistributedCache>());
        services.TryAddTransient<IClientCredentialsTokenCache, DistributedClientCredentialsTokenCache>();
        services.TryAddTransient<IClientCredentialsTokenEndpointService, ClientCredentialsTokenEndpointService>();
        services.TryAddTransient<IClientAssertionService, DefaultClientAssertionService>();

        services.TryAddTransient<IDPoPProofService, DefaultDPoPProofService>();
        services.TryAddTransient<IDPoPKeyStore, DefaultDPoPKeyStore>();
        services.TryAddTransient<IDPopProofRequestHandler, DPopProofRequestHandler>();

        // ** DistributedDPoPNonceStore **
        // By default, resolve the distributed cache for the DistributedClientCredentialsTokenCache
        // without key. If desired, a consumers can register the distributed cache with a key
        services.TryAddKeyedSingleton<IDistributedCache>(ServiceProviderKeys.DPoPNonceStore, (sp, _) => sp.GetRequiredService<IDistributedCache>());
        services.TryAddTransient<IDPoPNonceStore, DistributedDPoPNonceStore>();

#pragma warning restore CS0618 // Type or member is obsolete

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddTransient<ISendRequestRetryHandler, RetryWhenUnauthorizedHandler>();

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
    string tokenClientName,
    Action<HttpClient>? configureClient = null)
    {
        if (configureClient != null)
        {
            return services.AddHttpClient(httpClientName, configureClient)
                .AddClientCredentialsTokenHandler(tokenClientName);
        }

        return services.AddHttpClient(httpClientName)
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
        string tokenClientName,
        Action<IServiceProvider, HttpClient> configureClient) =>
            services.AddHttpClient(httpClientName, configureClient).AddClientCredentialsTokenHandler(tokenClientName);

    /// <summary>
    /// Adds the client access token handler to an HttpClient
    /// </summary>
    /// <param name="httpClientBuilder"></param>
    /// <param name="tokenClientName"></param>
    /// <returns></returns>
    public static IHttpClientBuilder AddClientCredentialsTokenHandler(
        this IHttpClientBuilder httpClientBuilder,
        string tokenClientName)
    {
        ArgumentNullException.ThrowIfNull(tokenClientName);

        return httpClientBuilder.AddHttpMessageHandler(provider =>
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var metrics = provider.GetRequiredService<AccessTokenManagementMetrics>();
#pragma warning restore CS0618 // Type or member is obsolete
            var dpopService = provider.GetRequiredService<IDPoPProofService>();
            var dpopNonceStore = provider.GetRequiredService<IDPoPNonceStore>();
            var accessTokenManagementService = provider.GetRequiredService<IClientCredentialsTokenManagementService>();

            var options = provider.GetRequiredService<IOptions<ClientCredentialsTokenManagementOptions>>();
            if (options.Value.UsePreviewExtensibilityOnAccessTokenHandlers)
            {
                var logger = provider.GetRequiredService<ILogger<ClientCredentialsTokenRetriever>>();

                var retriever = new ClientCredentialsTokenRetriever(accessTokenManagementService, tokenClientName);
                var dpopHandler = provider.GetRequiredService<IDPopProofRequestHandler>();
                var retryPolicy = provider.GetRequiredService<ISendRequestRetryHandler>();

                var accessTokenHandler = new AccessTokenHandler<ClientCredentialsTokenRetriever, ClientCredentialsToken>(
                    retryHandler: retryPolicy,
                    tokenRetriever: retriever,
                    dPoPProofRequestHandler: dpopHandler,
                    logger: logger);

                return accessTokenHandler;
            }
            else
            {
#pragma warning disable CS0618 // Type or member is obsolete

                var logger = provider.GetRequiredService<ILogger<ClientCredentialsTokenHandler>>();

                var clientCredentialsTokenHandler = new ClientCredentialsTokenHandler(
                    metrics: metrics,
                    dPoPProofService: dpopService,
                    dPoPNonceStore: dpopNonceStore,
                    accessTokenManagementService: accessTokenManagementService,
                    logger: logger,
                    tokenClientName: tokenClientName);

                return clientCredentialsTokenHandler;
#pragma warning restore CS0618 // Type or member is obsolete

            }
        });


    }

}
