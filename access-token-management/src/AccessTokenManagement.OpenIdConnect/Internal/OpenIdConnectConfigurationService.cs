// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.Types;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

/// <inheritdoc />

internal class OpenIdConnectConfigurationService(
    IOptions<UserTokenManagementOptions> userAccessTokenManagementOptions,
    IOptionsMonitor<OpenIdConnectOptions> oidcOptionsMonitor,
    IAuthenticationSchemeProvider schemeProvider) : IOpenIdConnectConfigurationService
{
    /// <inheritdoc />
    public async Task<OpenIdConnectClientConfiguration> GetOpenIdConnectConfigurationAsync(Scheme? schemeName = null)
    {
        var configScheme = schemeName ?? userAccessTokenManagementOptions.Value.ChallengeScheme;

        if (configScheme == null)
        {
            var defaultScheme = await schemeProvider.GetDefaultChallengeSchemeAsync().ConfigureAwait(false);

            if (defaultScheme is null)
            {
                throw new InvalidOperationException(
                    "No OpenID Connect authentication scheme configured for getting client configuration. Either set the scheme name explicitly or set the default challenge scheme");
            }

            configScheme = defaultScheme.Name;
        }

        var options = oidcOptionsMonitor.Get(configScheme.Value);

        OpenIdConnectConfiguration configuration;
        try
        {
            configuration = await options.ConfigurationManager!.GetConfigurationAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                $"Unable to load OpenID configuration for configured scheme: {e.Message}");
        }

        return new OpenIdConnectClientConfiguration
        {
            Scheme = configScheme,
            Authority = Authority.ParseOrDefault(options.Authority),
            TokenEndpoint = configuration.TokenEndpoint == null ? null : new Uri(configuration.TokenEndpoint),
            RevocationEndpoint = configuration.RevocationEndpoint == null ? null : new Uri(configuration.RevocationEndpoint),
            ClientId = ClientId.ParseOrDefault(options.ClientId),
            ClientSecret = ClientSecret.ParseOrDefault(options.ClientSecret),
            HttpClient = options.Backchannel,
        };
    }
}
