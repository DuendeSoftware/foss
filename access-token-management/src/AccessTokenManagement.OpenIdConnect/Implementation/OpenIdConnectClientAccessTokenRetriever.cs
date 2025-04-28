// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OTel;
using Microsoft.AspNetCore.Http;

namespace Duende.AccessTokenManagement.OpenIdConnect.Implementation;

/// <summary>
/// TokenRetriever that implements the logic on behalf of <see cref="AccessTokenHandler{TTokenRetriever,TToken}"/> to
/// retrieve a ClientCredentials Token for an OpenID Connect flow.
/// </summary>
/// <param name="httpContextAccessor"></param>
/// <param name="parameters"></param>
internal class OpenIdConnectClientAccessTokenRetriever(
    IHttpContextAccessor httpContextAccessor,
    UserTokenRequestParameters? parameters = null
) : ITokenRetriever<ClientCredentialsToken>
{
    private readonly UserTokenRequestParameters _parameters = parameters ?? new UserTokenRequestParameters();

    /// <inheritdoc />
    public async Task<ClientCredentialsToken> GetToken(HttpRequestMessage request, bool forceTokenRefresh, CancellationToken cancellationToken)
    {
        var userTokenRequestParameters = new UserTokenRequestParameters
        {
            ChallengeScheme = _parameters.ChallengeScheme,
            Scope = _parameters.Scope,
            Resource = _parameters.Resource,
            Parameters = _parameters.Parameters,
            Assertion = _parameters.Assertion,
            Context = _parameters.Context,
            ForceRenewal = forceTokenRefresh,
        };

        if (httpContextAccessor.HttpContext == null)
        {
            throw new InvalidOperationException("HttpContext is null");
        }

        return await httpContextAccessor.HttpContext.GetClientAccessTokenAsync(
                userTokenRequestParameters,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public AccessTokenManagementMetrics.TokenRequestType TokenRequestType { get; } = AccessTokenManagementMetrics.TokenRequestType.ClientCredentials;
}
