// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement;

internal class RetryOnUnauthorizedPolicy
(
    ILogger<RetryOnUnauthorizedPolicy> logger) : ISendRequestRetryPolicy
{
    public async Task<HttpResponseMessage> Handle(
        HttpRequestMessage request,
        SendRequestWithToken sendRequestWithToken,
        CancellationToken cancellationToken)
    {
        var response = await sendRequestWithToken(new AccessTokenHandlerRequestData()
        {
            Request = request
        }, cancellationToken).ConfigureAwait(false);

        var dPoPNonce = response.GetDPoPNonce();

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            var forceTokenRefresh = !response.IsDPoPError();
            if (!forceTokenRefresh && !string.IsNullOrEmpty(dPoPNonce))
            {
                logger.RequestFailedWithDPoPErrorWillRetry(response.GetDPoPError());
            }
            else
            {
                logger.TokenNotAcceptedWhenSendingRequest();
            }

            response = await sendRequestWithToken(new AccessTokenHandlerRequestData()
            {
                Request = request,
                DPoPNonce = dPoPNonce,
                ForceTokenRefresh = forceTokenRefresh
            }, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }
}

public delegate Task<HttpResponseMessage> SendRequestWithToken(AccessTokenHandlerRequestData data, CancellationToken cancellationToken);

/// <summary>
/// Captures information about requests being sent via access token handler,
/// so that the retry policy and the access token handler can work together 
/// </summary>
public record AccessTokenHandlerRequestData
{
    public required HttpRequestMessage Request { get; init; }
    public string? DPoPNonce { get; init; }
    public bool ForceTokenRefresh { get; init; }
}
