// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Builder for client credential clients
/// </summary>
public sealed class ClientCredentialsTokenManagementBuilder(IServiceCollection services)
{
    /// <summary>
    /// The service collection being configured.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Adds a client credentials client to the token management system
    /// </summary>
    /// <param name="name">The logical name of the client configuration.</param>
    /// <param name="configureOptions">A delegate that configures the named <see cref="ClientCredentialsClient"/> options.</param>
    /// <returns>The same builder instance for chaining.</returns>
    public ClientCredentialsTokenManagementBuilder AddClient(string name,
        Action<ClientCredentialsClient> configureOptions)
    {
        Services.Configure(name, configureOptions);

        return this;
    }
}
