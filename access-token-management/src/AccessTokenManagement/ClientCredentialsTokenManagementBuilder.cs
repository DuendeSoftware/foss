// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Builder for client credential clients
/// </summary>
public sealed class ClientCredentialsTokenManagementBuilder(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Adds a client credentials client to the token management system
    /// </summary>
    /// <param name="name"></param>
    /// <param name="configureOptions"></param>
    /// <returns></returns>
    public ClientCredentialsTokenManagementBuilder AddClient(ClientCredentialsClientName name,
        Action<ClientCredentialsClient> configureOptions)
    {
        Services
            .Configure(name, configureOptions);

        return this;
    }
}
