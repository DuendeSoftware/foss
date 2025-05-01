// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OTel;

namespace Duende.AccessTokenManagement.OpenIdConnect.Implementation;

internal class OpenIdConnectUserAccessTokenRetriever(
    IUserAccessor userAccessor,
    IUserTokenManagementService userTokenManagement,
    UserTokenRequestParameters? parameters = null
) : ITokenRetriever
{
    private readonly UserTokenRequestParameters _parameters = parameters ?? new UserTokenRequestParameters();

    public async Task<ClientCredentialsToken> GetToken(HttpRequestMessage request, bool forceTokenRefresh, CancellationToken cancellationToken)
    {
        var parameters = new UserTokenRequestParameters
        {
            SignInScheme = _parameters.SignInScheme,
            ChallengeScheme = _parameters.ChallengeScheme,
            Resource = _parameters.Resource,
            Context = _parameters.Context,
            ForceRenewal = forceTokenRefresh,
        };

        var user = await userAccessor.GetCurrentUserAsync().ConfigureAwait(false);

        var token = await userTokenManagement.GetAccessTokenAsync(user, parameters, cancellationToken).ConfigureAwait(false);
        return token;
    }
    public AccessTokenManagementMetrics.TokenRequestType TokenRequestType { get; } = AccessTokenManagementMetrics.TokenRequestType.User;
}
