// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

/// <summary>
/// Governs how to request dpop proofs and how to handle the dpop responses from requests.
/// </summary>
public interface IDPopProofRequestHandler
{
    /// <summary>
    /// Try to acquire dpop proof for the given request parameters
    /// </summary>
    /// <param name="parameters">The request parameters</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>True if dpop proof was aquired.</returns>
    Task<bool> TryAcquireDPopProof(DPopProofRequestParameters parameters, CancellationToken cancellationToken);

    /// <summary>
    /// When a request is sent, the dpop response should be handled. This typically means
    /// storing the dpop nonce in a store. 
    /// </summary>
    /// <param name="response">The response message that likely contains the dpop nonce.</param>
    /// <param name="cancellationToken">cancellationt oken</param>
    /// <returns></returns>
    Task HandleDPopResponse(HttpResponseMessage response, CancellationToken cancellationToken);

}
