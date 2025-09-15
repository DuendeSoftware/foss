// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OTel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Duende.AccessTokenManagement.Internal;

/// <summary>
/// Represents the resiliency policies that we implement. 
/// </summary>
internal static class Resiliency
{
    /// <summary>
    /// Adds the default resiliency policies for access token handling.
    /// 
    /// Basically, this retries http calls that return 401 Unauthorized.
    /// 
    /// When using DPOP, it will retry the call if the nonce is missing or if the nonce is invalid.
    /// it will then also take then once from the response and put it on the request. 
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public static ResiliencePipelineBuilder<HttpResponseMessage> AddDefaultAccessTokenHandlingResiliency(
        this ResiliencePipelineBuilder<HttpResponseMessage> builder, ResilienceHandlerContext context)
    {
        var metrics = context.ServiceProvider.GetRequiredService<AccessTokenManagementMetrics>();

        return builder
            .AddRetry(new HttpRetryStrategyOptions()
            {
                MaxRetryAttempts = 1,
                ShouldHandle = arguments =>
                {
                    var response = arguments.Outcome.Result;
                    if (response == null)
                    {
                        // No result (e.g. exception)
                        // we don't retry in that case. 
                        return ValueTask.FromResult(false);
                    }

                    // Only retry on 401 Unauthorized
                    if (response.StatusCode != HttpStatusCode.Unauthorized)
                    {
                        return ValueTask.FromResult(false);
                    }

                    if (response.IsDPoPError())
                    {
                        metrics.DPoPNonceErrorRetry(response.RequestMessage?.GetClientId(), response.GetDPoPError());
                        var dPoPNonce = response.GetDPoPNonceValue();

                        // When we get a DPoP error, we need to retry with the nonce from the response.
                        if (dPoPNonce == null)
                        {
                            // No nonce in the response, we can't retry.
                            return ValueTask.FromResult(false);
                        }

                        // Put the nonce on the request, so that this can be used in the retried request. 
                        response.RequestMessage?.SetDPoPNonce(dPoPNonce.Value);

                        return ValueTask.FromResult(true);
                    }

                    metrics.AccessTokenAccessDeniedRetry(null);
                    // We received a 401 Unauthorized, but not a DPoP error.
                    // this indicates that likely the access token is either invalid
                    // or expired. To compensate for expired tokens, we retry once. 
                    response.RequestMessage?.SetForceRenewal(true);
                    return ValueTask.FromResult(true);
                }
            });
    }
}
