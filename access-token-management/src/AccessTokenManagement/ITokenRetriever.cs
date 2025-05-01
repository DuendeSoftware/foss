// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OTel;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Interface for retrieving access tokens, on behalf of <see cref="AccessTokenRequestHandler"/>
/// </summary>

public interface ITokenRetriever
{
    /// <summary>
    /// Method that retrieves the actual access token
    /// </summary>
    /// <param name="request"></param>
    /// <param name="forceTokenRefresh"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ClientCredentialsToken> GetToken(HttpRequestMessage request, bool forceTokenRefresh, CancellationToken cancellationToken);

    /// <summary>
    /// The type of token that's requested, for metrics
    /// </summary>
    abstract AccessTokenManagementMetrics.TokenRequestType TokenRequestType { get; }
}
