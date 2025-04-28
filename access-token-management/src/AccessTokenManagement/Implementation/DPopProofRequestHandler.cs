// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement.Implementation;

/// <summary>
/// Hanldes Dpop proof requests for http requests. 
/// </summary>
/// <param name="dPoPNonceStore"></param>
/// <param name="dPoPProofService"></param>
/// <param name="logger"></param>
internal sealed class DPopProofRequestHandler(
    IDPoPNonceStore dPoPNonceStore,
    IDPoPProofService dPoPProofService,
    ILogger<DPopProofRequestHandler> logger) : IDPopProofRequestHandler
{
    public async Task<bool> TryAcquireDPopProof(DPopProofRequestParameters parameters,
        CancellationToken cancellationToken)
    {
        var request = parameters.Request;
        
        request.ClearDPoPProofToken();

        var token = parameters.ClientCredentialsToken;
        if (string.IsNullOrEmpty(token.DPoPJsonWebKey))
        {
            return false;
        }
        request.TryGetDPopProofAdditionalPayloadClaims(out var additionalClaims);

        var dPoPProofRequest = new DPoPProofRequest
        {
            AccessToken = token.AccessToken,
            Url = request.GetDPoPUrl(),
            Method = request.Method.ToString(),
            DPoPJsonWebKey = token.DPoPJsonWebKey,
            DPoPNonce = parameters.DPoPNonce,
            AdditionalPayloadClaims = additionalClaims,
        };
        var proofToken = await dPoPProofService.CreateProofTokenAsync(dPoPProofRequest).ConfigureAwait(false);

        if (proofToken == null)
        {
            logger.FailedToCreateDPopProofToken(request.RequestUri?.AbsoluteUri);
            return false;
        }

        logger.SendingDPoPProofToken(request.RequestUri?.AbsoluteUri);
        request.SetDPoPProofToken(proofToken.ProofToken);
        return true;
    }

    public async Task HandleDPopResponse(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var request = response.RequestMessage;

        if (request == null || response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return;
        }
        var dPoPNonce = response.GetDPoPNonce();

        if (dPoPNonce == null)
            return;

        var dPoPNonceContext = new DPoPNonceContext
        {
            Url = request.GetDPoPUrl(),
            Method = request.Method.ToString(),
        };
        await dPoPNonceStore.StoreNonceAsync(dPoPNonceContext, dPoPNonce, cancellationToken);
    }
}
