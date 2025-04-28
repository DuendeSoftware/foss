// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Retries requests that somehow return unauthorized. Two conditions are expected:
/// 1. Access token has expired.
/// 2. Dpop nonce is invalid or missing and should be sent. 
/// </summary>
/// <param name="logger"></param>
internal class RetryWhenUnauthorizedHandler
(
    ILogger<RetryWhenUnauthorizedHandler> logger) : ISendRequestRetryHandler
{
    public async Task<HttpResponseMessage> Handle(
        HttpRequestMessage request,
        SendRequestWithToken sendRequestWithToken,
        CancellationToken cancellationToken)
    {
        var response = await sendRequestWithToken(new AccessTokenHandlerRequestParameters()
        {
            Request = request
        }, cancellationToken).ConfigureAwait(false);

        var dPoPNonce = response.GetDPoPNonce();

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var forceTokenRefresh = !response.IsDPoPError();
            if (!forceTokenRefresh && !string.IsNullOrEmpty(dPoPNonce))
            {
                logger.RequestFailedWithDPoPErrorWillRetry(response.GetDPoPError());
            }
            else
            {
                logger.TokenNotAcceptedWhenSendingRequest();
            }

            // Dispose the previous request and send it again. 
            response.Dispose();
            response = await sendRequestWithToken(new AccessTokenHandlerRequestParameters()
            {
                Request = request,
                DPoPNonce = dPoPNonce,
                ForceTokenRefresh = forceTokenRefresh
            }, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }
}
