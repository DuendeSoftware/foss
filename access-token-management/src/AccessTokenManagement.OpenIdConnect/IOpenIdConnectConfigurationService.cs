// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Service to extract necessary configuration from an OIDC handler
/// </summary>
public interface IOpenIdConnectConfigurationService
{
    /// <summary>
    /// Reads the configuration from either the default challenge scheme or a named scheme
    /// </summary>
    /// <param name="schemeName"></param>
    /// <returns></returns>
    public Task<OpenIdConnectClientConfiguration> GetOpenIdConnectConfigurationAsync(string? schemeName = null);
}
