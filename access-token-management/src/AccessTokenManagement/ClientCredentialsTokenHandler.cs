// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OTel;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Delegating handler that injects a client credentials access token into an outgoing request
/// </summary>
[Obsolete(Constants.AtmPublicSurfaceInternal, UrlFormat = Constants.AtmPublicSurfaceLink)]
public class ClientCredentialsTokenHandler(
    AccessTokenManagementMetrics metrics,
    IDPoPProofService dPoPProofService,
    IDPoPNonceStore dPoPNonceStore,
    IClientCredentialsTokenManagementService accessTokenManagementService,
    ILogger<ClientCredentialsTokenHandler> logger,
    string tokenClientName)
    : AccessTokenHandler(metrics, dPoPProofService, dPoPNonceStore, logger)
{
    /// <inheritdoc/>
    protected override Task<ClientCredentialsToken> GetAccessTokenAsync(bool forceRenewal, CancellationToken cancellationToken)
    {
        var parameters = new TokenRequestParameters
        {
            ForceRenewal = forceRenewal
        };
        return accessTokenManagementService.GetAccessTokenAsync(tokenClientName, parameters, cancellationToken);
    }

    protected override AccessTokenManagementMetrics.TokenRequestType TokenRequestType => AccessTokenManagementMetrics.TokenRequestType.ClientCredentials;
}
