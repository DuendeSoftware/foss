// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.Internal;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

/// <summary>
/// Delegating handler that adds behavior needed for DPoP to the backchannel
/// http client of the OIDC authentication handler.
///
/// This handler has two main jobs:
///
/// 1. Store new nonces from successful responses from the authorization server.
///
/// 2. Attach proof tokens to token requests in the code flow.
///
///    On the authorize request, we will have sent a dpop_jkt parameter with a
///    key thumbprint. The AS expects that we will use the corresponding key to
///    create our proof, and we track that key in the http context. This handler
///    retrieves that key and uses it to create proof tokens for use in the code
///    flow.
///
///    Additionally, the token endpoint might respond to a token exchange
///    request with a request to retry with a nonce that it supplies via http
///    header. When it does, this handler retries those code exchange requests.
///
/// </summary>
internal class AuthorizationServerDPoPHandler(
    IDPoPProofService dPoPProofService,
    IDPoPNonceStore dPoPNonceStore,
    IHttpContextAccessor httpContextAccessor,
    ILoggerFactory loggerFactory) : DelegatingHandler
{
    // We depend on the logger factory, rather than the logger itself, since
    // the type parameter of the logger (referencing this class) will not
    // always be accessible.
    private readonly ILogger<AuthorizationServerDPoPHandler> _logger = loggerFactory.CreateLogger<AuthorizationServerDPoPHandler>();

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CT ct)
    {
        var codeExchangeJwk = httpContextAccessor.HttpContext?.GetCodeExchangeDPoPKey();
        if (codeExchangeJwk != null)
        {
            await SetDPoPProofTokenForCodeExchangeAsync(request, jwk: codeExchangeJwk).ConfigureAwait(false);
        }

        var response = await base.SendAsync(request, ct).ConfigureAwait(false);

        // The authorization server might send us a new nonce on either a success or failure
        var dPoPNonce = response.GetDPoPNonceValue();

        if (dPoPNonce == null)
        {
            return response;
        }

        // This handler contains specialized logic to create the new proof
        // token using the proof key that was associated with a code flow
        // using a dpop_jkt parameter on the authorize call. Other flows
        // (such as refresh), are separately responsible for retrying with a
        // server-issued nonce. So, we ONLY do the retry logic when we have
        // the dpop_jkt's jwk
        if (codeExchangeJwk != null)
        {
            // If the http response code indicates a bad request, we can infer
            // that we should retry with the new nonce.
            //
            // The server should have also set the error: use_dpop_nonce, but
            // there's no need to incur the cost of parsing the json and
            // checking for that, as we would only receive the nonce http header
            // when that error was set. Authorization servers might preemptively
            // send a new nonce, but the spec specifically says to do that on a
            // success (and we handle that case in the else block).
            //
            // TL;DR - presence of nonce and 400 response code is enough to
            // trigger a retry during code exchange
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                _logger.DPoPErrorDuringTokenRefreshWillRetryWithServerNonce(LogLevel.Debug, response.GetDPoPError());
                response.Dispose();
                await SetDPoPProofTokenForCodeExchangeAsync(request, dPoPNonce, codeExchangeJwk).ConfigureAwait(false);
                return await base.SendAsync(request, ct).ConfigureAwait(false);
            }
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            _logger.FailedToGetDPoPNonce(LogLevel.Debug, response.StatusCode);
            return response;
        }

        _logger.AuthorizationServerSuppliedNewNonce(LogLevel.Debug);

        await dPoPNonceStore.StoreNonceAsync(new DPoPNonceContext
        {
            Url = request.GetDPoPUri(),
            Method = request.Method,
        }, dPoPNonce.Value, ct);

        return response;
    }

    /// <summary>
    /// Creates a DPoP proof token and attaches it to a request.
    /// </summary>
    internal async Task SetDPoPProofTokenForCodeExchangeAsync(HttpRequestMessage request, DPoPNonce? dpopNonce = null, DPoPProofKey? jwk = null)
    {
        if (jwk == null)
        {
            return;
        }

        request.ClearDPoPProofToken();

        var proofToken = await dPoPProofService.CreateProofTokenAsync(new DPoPProofRequest
        {
            Url = request.GetDPoPUri(),
            Method = request.Method,
            DPoPProofKey = jwk.Value,
            DPoPNonce = dpopNonce,
        });

        if (proofToken != null)
        {
            _logger.SendingDPoPProofToken(LogLevel.Debug, request.RequestUri);
            request.SetDPoPProofToken(proofToken.Value);
        }
        else
        {
            _logger.FailedToCreateDPopProofToken(LogLevel.Debug, request.RequestUri);
        }
    }
}
