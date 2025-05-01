// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

/// <summary>
/// Named options to synthesize client credentials based on OIDC handler configuration
/// </summary>
internal class ConfigureOpenIdConnectClientCredentialsOptions(
    IOpenIdConnectConfigurationService configurationService,
    IOptions<UserTokenManagementOptions> options) : IConfigureNamedOptions<ClientCredentialsClient>
{
    private readonly UserTokenManagementOptions _options = options.Value;
    /// <inheritdoc />
    public void Configure(ClientCredentialsClient options)
    { }

    /// <inheritdoc />
    public void Configure(string? name, ClientCredentialsClient options)
    {
        if (name.IsMissing())
        {
            return;
        }

        if (!name.StartsWith(OpenIdConnectTokenManagementDefaults.ClientCredentialsClientNamePrefix))
        {
            return;
        }

        string? scheme = null;
        if (name.Length > OpenIdConnectTokenManagementDefaults.ClientCredentialsClientNamePrefix.Length)
        {
            scheme = name[OpenIdConnectTokenManagementDefaults.ClientCredentialsClientNamePrefix.Length..];
        }

        if (string.IsNullOrWhiteSpace(scheme))
        {
            throw new ArgumentException("Missing scheme when used with OpenIdConnectTokenManagementDefaults.ClientCredentialsClientNamePrefix");
        }

        var oidc = configurationService.GetOpenIdConnectConfigurationAsync(scheme).GetAwaiter().GetResult();

        options.TokenEndpoint = oidc.TokenEndpoint;
        options.ClientId = oidc.ClientId;
        options.ClientSecret = oidc.ClientSecret;
        options.ClientCredentialStyle = _options.ClientCredentialStyle;
        options.Scope = _options.ClientCredentialsScope;
        options.Resource = _options.ClientCredentialsResource;
        options.HttpClient = oidc.HttpClient;
        options.DPoPJsonWebKey = _options.DPoPJsonWebKey;
    }
}
