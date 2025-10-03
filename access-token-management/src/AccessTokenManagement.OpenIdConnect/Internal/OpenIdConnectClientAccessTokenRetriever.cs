// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

/// <summary>
/// A token retriever that uses the configuration in openid connect to retrieve client credential access tokens
/// </summary>
internal class OpenIdConnectClientAccessTokenRetriever(
    IClientCredentialsTokenManager tokenManager,
    IOptions<UserTokenManagementOptions> options,
    IAuthenticationSchemeProvider schemeProvider,
    UserTokenRequestParameters? parameters = null,
    ITokenRequestCustomizer? customizer = null)
    : AccessTokenRequestHandler.ITokenRetriever
{
    private readonly UserTokenRequestParameters _parameters = parameters ?? new UserTokenRequestParameters();

    public async Task<TokenResult<AccessTokenRequestHandler.IToken>> GetTokenAsync(HttpRequestMessage request, CT ct)
    {
        var baseParameters = new UserTokenRequestParameters
        {
            ChallengeScheme = _parameters.ChallengeScheme,
            Scope = _parameters.Scope,
            Resource = _parameters.Resource,
            Parameters = _parameters.Parameters,
            Assertion = _parameters.Assertion,
            Context = _parameters.Context,
            ForceTokenRenewal = request.GetForceRenewal()
        };

        var customizedParameters = customizer != null
            ? await customizer.Customize(request, baseParameters, ct)
            : baseParameters;

        var userTokenRequestParameters = baseParameters with
        {
            Scope = customizedParameters.Scope ?? _parameters.Scope,
            Resource = customizedParameters.Resource ?? _parameters.Resource,
            Parameters = customizedParameters.Parameters,
            Assertion = customizedParameters.Assertion,
            Context = customizedParameters.Context,
            ForceTokenRenewal = customizedParameters.ForceTokenRenewal
        };

        var schemeName = userTokenRequestParameters.ChallengeScheme ?? options.Value.ChallengeScheme;

        if (schemeName == null)
        {
            var defaultScheme = await schemeProvider.GetDefaultChallengeSchemeAsync().ConfigureAwait(false);
            if (defaultScheme == null)
            {
                throw new InvalidOperationException(
                    "Cannot retrieve client access token. No scheme was provided and default challenge scheme was not set.");
            }

            schemeName = Scheme.Parse(defaultScheme.Name);
        }

        var getTokenResult = await tokenManager.GetAccessTokenAsync(
            schemeName.Value.ToClientName(),
            userTokenRequestParameters,
            ct).ConfigureAwait(false);

        if (getTokenResult.WasSuccessful(out var token, out var error))
        {
            return token;
        }

        return error;
    }
}
