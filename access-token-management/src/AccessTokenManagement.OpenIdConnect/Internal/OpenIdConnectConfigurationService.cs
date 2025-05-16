// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


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
    public async Task<OpenIdConnectClientConfiguration> GetOpenIdConnectConfigurationAsync(
        Scheme? schemeName = null,
        CT ct = default)
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

        var options = oidcOptionsMonitor.Get(configScheme.ToString());

        OpenIdConnectConfiguration configuration;
        try
        {
            configuration = await options.ConfigurationManager!.GetConfigurationAsync(ct).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                $"Unable to load OpenID configuration for configured scheme: {e.Message}");
        }

        if (configuration.TokenEndpoint == null)
        {
            throw new InvalidOperationException("Tokenendpoint is null");
        }

        return new OpenIdConnectClientConfiguration
        {
            Scheme = configScheme,
            TokenEndpoint = new Uri(configuration.TokenEndpoint),
            RevocationEndpoint = configuration.RevocationEndpoint == null ? null : new Uri(configuration.RevocationEndpoint),
            ClientId = ClientId.Parse(options.ClientId ?? throw new InvalidOperationException("ClientId is null")),
            ClientSecret = ClientSecret.Parse(options.ClientSecret ?? throw new InvalidOperationException("ClientSecret is null")),
            HttpClient = options.Backchannel,
        };
    }
}
