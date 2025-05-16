// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using Duende.AccessTokenManagement.DPoP;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement.Internal;

/// <summary>
/// Handles Dpop proof requests for http requests.
/// </summary>
/// <param name="dPoPNonceStore"></param>
/// <param name="dPoPProofService"></param>
/// <param name="logger"></param>
internal sealed class DPopProofRequestHandler(
    IDPoPNonceStore dPoPNonceStore,
    IDPoPProofService dPoPProofService,
    ILogger<DPopProofRequestHandler> logger) : IDPopProofRequestHandler
{
    public async Task<bool> TryAcquireDPopProofAsync(DPopProofRequestParameters parameters,
        CT ct)
    {
        var request = parameters.Request;

        request.ClearDPoPProofToken();

        var token = parameters.AccessToken;
        if (token.DPoPJsonWebKey == null)
        {
            return false;
        }
        request.TryGetDPopProofAdditionalPayloadClaims(out var additionalClaims);

        var dPoPProofRequest = new DPoPProof
        {
            AccessToken = token.AccessToken,
            Url = request.GetDPoPUrl(),
            Method = request.Method,
            ProofKey = token.DPoPJsonWebKey.Value,
            DPoPNonce = parameters.DPoPNonce,
            AdditionalPayloadClaims = additionalClaims,
        };
        var proofToken = await dPoPProofService.CreateProofTokenAsync(dPoPProofRequest, ct).ConfigureAwait(false);

        if (proofToken == null)
        {
            logger.FailedToCreateDPopProofToken(LogLevel.Debug, request.RequestUri);
            return false;
        }

        logger.SendingDPoPProofToken(LogLevel.Debug, request.RequestUri);
        request.SetDPoPProofToken(proofToken.Value);
        return true;
    }

    public async Task HandleDPopResponseAsync(HttpResponseMessage response, CT ct)
    {
        var request = response.RequestMessage;

        if (request == null || response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return;
        }
        var dPoPNonce = response.GetDPoPNonce();

        if (dPoPNonce == null)
        {
            return;
        }

        var dPoPNonceContext = new DPoPNonceContext
        {
            Url = request.GetDPoPUrl(),
            Method = request.Method,
        };
        logger.AuthorizationServerSuppliedNewNonce(LogLevel.Debug);
        await dPoPNonceStore.StoreNonceAsync(dPoPNonceContext, dPoPNonce.Value, ct);
    }
}
