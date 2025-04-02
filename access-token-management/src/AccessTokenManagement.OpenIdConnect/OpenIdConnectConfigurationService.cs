// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <inheritdoc />
[Obsolete(Constants.AtmPublicSurfaceInternal, UrlFormat = Constants.AtmPublicSurfaceLink)]
public class OpenIdConnectConfigurationService(
    IOptions<UserTokenManagementOptions> userAccessTokenManagementOptions,
    IOptionsMonitor<OpenIdConnectOptions> oidcOptionsMonitor,
    IAuthenticationSchemeProvider schemeProvider) : IOpenIdConnectConfigurationService
{
    /// <inheritdoc />
    public async Task<OpenIdConnectClientConfiguration> GetOpenIdConnectConfigurationAsync(string? schemeName = null)
    {
        var configScheme = schemeName ?? userAccessTokenManagementOptions.Value.ChallengeScheme;

        if (string.IsNullOrWhiteSpace(configScheme))
        {
            var defaultScheme = await schemeProvider.GetDefaultChallengeSchemeAsync().ConfigureAwait(false);

            if (defaultScheme is null)
            {
                throw new InvalidOperationException(
                    "No OpenID Connect authentication scheme configured for getting client configuration. Either set the scheme name explicitly or set the default challenge scheme");
            }

            configScheme = defaultScheme.Name;
        }

        var options = oidcOptionsMonitor.Get(configScheme);

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

            Authority = options.Authority,
            TokenEndpoint = configuration.TokenEndpoint,
            RevocationEndpoint = configuration.RevocationEndpoint,
            ClientId = options.ClientId,
            ClientSecret = options.ClientSecret,
            HttpClient = options.Backchannel,
        };
    }
}
