// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OTel;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Delegating handler that injects the current access token into an outgoing request
/// </summary>
[Obsolete(Constants.AtmPublicSurfaceInternal, UrlFormat = Constants.AtmPublicSurfaceLink)]
public class OpenIdConnectUserAccessTokenHandler(
    AccessTokenManagementMetrics metrics,
    IDPoPProofService dPoPProofService,
    IDPoPNonceStore dPoPNonceStore,
    IUserAccessor userAccessor,
    IUserTokenManagementService userTokenManagement,
    ILogger<OpenIdConnectClientAccessTokenHandler> logger,
    UserTokenRequestParameters? parameters = null)
    : AccessTokenHandler(metrics, dPoPProofService, dPoPNonceStore, logger)
{
    private readonly UserTokenRequestParameters _parameters = parameters ?? new UserTokenRequestParameters();
    /// <inheritdoc/>
    protected override async Task<ClientCredentialsToken> GetAccessTokenAsync(bool forceRenewal, CancellationToken cancellationToken)
    {
        var parameters = new UserTokenRequestParameters
        {
            SignInScheme = _parameters.SignInScheme,
            ChallengeScheme = _parameters.ChallengeScheme,
            Resource = _parameters.Resource,
            Context = _parameters.Context,
            ForceRenewal = forceRenewal,
        };

        var user = await userAccessor.GetCurrentUserAsync().ConfigureAwait(false);

        var token = await userTokenManagement.GetAccessTokenAsync(user, parameters, cancellationToken).ConfigureAwait(false);
        return token;
    }

    protected override AccessTokenManagementMetrics.TokenRequestType TokenRequestType => AccessTokenManagementMetrics.TokenRequestType.User;
}
