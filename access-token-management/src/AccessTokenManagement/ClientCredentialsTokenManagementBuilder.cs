// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.Implementation;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Builder for client credential clients
/// </summary>
public class ClientCredentialsTokenManagementBuilder(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Adds a client credentials client to the token management system
    /// </summary>
    /// <param name="name"></param>
    /// <param name="configureOptions"></param>
    /// <returns></returns>
    public ClientCredentialsTokenManagementBuilder AddClient(string name,
        Action<ClientCredentialsClient> configureOptions)
    {
        Services.Configure(name, configureOptions);
        return this;
    }

    public ClientCredentialsTokenManagementBuilder UsePreviewHybridCache()
    {
        // Replace the default implementations with the hybrid cache versions
#pragma warning disable CS0618 // Type or member is obsolete
        RemoveDefaultRegistration<IClientCredentialsTokenCache, DistributedClientCredentialsTokenCache>();
        Services.AddTransient<IClientCredentialsTokenCache, HybridClientCredentialsTokenCache>();

        RemoveDefaultRegistration<IDPoPNonceStore, DistributedDPoPNonceStore>();
        Services.AddTransient<IDPoPNonceStore, HybridDPoPNonceStore>();

#pragma warning restore CS0618 // Type or member is obsolete

        // Make sure the hybrid cache is registered. If this is registered by the user, this will be a no-op
        Services.AddHybridCache();

        // The cache and nonce store don't consume the cache directly, but via a redirect. 
        // This allows the consumer to register a custom hybrid cache with a key if desired.
        // By default, consume the non-keyed hybrid cache. 
        Services.TryAddKeyedSingleton<HybridCache>(
            serviceKey: ServiceProviderKeys.ClientCredentialsTokenCache,
            implementationFactory: (sp, _) => sp.GetRequiredService<HybridCache>());

        Services.TryAddKeyedSingleton<HybridCache>(
            serviceKey: ServiceProviderKeys.DPoPNonceStore,
            implementationFactory: (sp, _) => sp.GetRequiredService<HybridCache>());

        return this;
    }

    private void RemoveDefaultRegistration<TService, TImplementation>()
    {
        var existingRegistration = Services.FirstOrDefault(
            x =>
                !x.IsKeyedService && x.ServiceType == typeof(TService) &&
                x.ImplementationType == typeof(TImplementation) &&
                x is
                {
                    Lifetime: ServiceLifetime.Transient,
                    ImplementationFactory: null,
                    ImplementationInstance: null
                });

        if (existingRegistration != null)
        {
            Services.Remove(existingRegistration);
        }
    }
}
