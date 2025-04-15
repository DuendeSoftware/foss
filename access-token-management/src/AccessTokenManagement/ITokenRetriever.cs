// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OTel;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Interface for retrieving access tokens, on behalf of <see cref="AccessTokenHandler{TTokenRetriever,TToken}"/>
/// </summary>
/// <typeparam name="T">The type of token to retrieve</typeparam>
public interface ITokenRetriever<T> where T : ClientCredentialsToken
{
    /// <summary>
    /// Method that retrieves the actual access token
    /// </summary>
    /// <param name="request"></param>
    /// <param name="forceTokenRefresh"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<T> GetToken(HttpRequestMessage request, bool forceTokenRefresh, CancellationToken cancellationToken);

    /// <summary>
    /// The type of token that's requested, for metrics
    /// </summary>
    abstract AccessTokenManagementMetrics.TokenRequestType TokenRequestType { get; }
}
