// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement;

namespace Microsoft.Extensions.DependencyInjection;

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
    public ClientCredentialsTokenManagementBuilder AddClient(string name, Action<ClientCredentialsClient> configureOptions)
    {
        Services.Configure(name, configureOptions);
        return this;
    }
}
