// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.Internal;
using Duende.AccessTokenManagement.OTel;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement;

/// <summary>
/// This HttpMessageHandler is used to acquire and add an access token while sending
/// http requests. The method on how to acquire tokens, the retry policy and the dpop handling
/// can be customized. 
/// </summary>
public sealed class AccessTokenRequestHandler(
    IDPoPNonceStore dPoPNonceStore,
    IDPoPProofService dPoPProofService,
    AccessTokenRequestHandler.ITokenRetriever tokenRetriever,
    ILogger<AccessTokenRequestHandler> logger)
    : DelegatingHandler
{
    protected override HttpResponseMessage Send(HttpRequestMessage request, CT ct) =>
        throw new NotSupportedException(
            "The (synchronous) Send() method is not supported. Please use the async SendAsync variant. ");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CT ct)
    {
        TokenResult<IToken> tokenResult;

        // ReSharper disable once ExplicitCallerInfoArgument
        using (ActivitySources.Main.StartActivity(ActivityNames.AcquiringToken))
        {
            tokenResult = await tokenRetriever.GetTokenAsync(request, ct);
        }

        if (!tokenResult.WasSuccessful(out var token, out var failure))
        {
            logger.FailedToObtainAccessTokenWhileSendingRequest(LogLevel.Warning, failure.Error, failure.ErrorDescription);

            // Cant acquire token, so sending without
            return await base.SendAsync(request, ct);
        }

        var scheme = token.AccessTokenType?.ToScheme() ?? Scheme.Bearer;

        if (token.DPoPJsonWebKey != null)
        {
            // looks like this is a DPoP bound token, so try to generate the proof token
            if (!await TryAcquireDPopProofAsync(request, token, ct))
            {
                // failed or opted out for this request, to fall back to Bearer 
                scheme = Scheme.Bearer;
            }
        }
        request.SetToken(scheme, token);

        // Send the actual request with the access token. 
        var httpResponseMessage = await base.SendAsync(request, ct);

        // On the response, there may be a DPOP Nonce that we need to store. 
        await HandleDPopResponseAsync(httpResponseMessage, ct);

        return httpResponseMessage;
    }


    private async Task<bool> TryAcquireDPopProofAsync(HttpRequestMessage request, IToken token, CT ct)
    {
        request.ClearDPoPProofToken();

        if (token.DPoPJsonWebKey == null)
        {
            return false;
        }
        request.TryGetDPopProofAdditionalPayloadClaims(out var additionalClaims);

        var dPoPProofRequest = new DPoPProofRequest
        {
            AccessToken = token.AccessToken,
            Url = request.GetDPoPUri(),
            Method = request.Method,
            DPoPProofKey = token.DPoPJsonWebKey.Value,
            DPoPNonce = request.GetDPoPNonce(),
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

    private async Task HandleDPopResponseAsync(HttpResponseMessage response, CT ct)
    {
        var request = response.RequestMessage;

        if (request == null || response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return;
        }
        var dPoPNonce = response.GetDPoPNonceValue();

        if (dPoPNonce == null)
        {
            return;
        }

        var dPoPNonceContext = new DPoPNonceContext
        {
            Url = request.GetDPoPUri(),
            Method = request.Method,
        };
        logger.AuthorizationServerSuppliedNewNonce(LogLevel.Debug);
        await dPoPNonceStore.StoreNonceAsync(dPoPNonceContext, dPoPNonce.Value, ct);
    }
    /// <summary>
    /// Interface for retrieving access tokens, on behalf of <see cref="AccessTokenRequestHandler"/>
    /// </summary>

    public interface ITokenRetriever
    {
        /// <summary>
        /// Method that retrieves the actual access token
        /// </summary>
        /// <param name="request"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<TokenResult<IToken>> GetTokenAsync(
            HttpRequestMessage request,
            CT ct);
    }

    /// <summary>
    /// Common information needed by access token handler
    /// </summary>
    public interface IToken
    {
        AccessToken AccessToken { get; }

        /// <summary>
        /// The string representation of the JSON web key to use for DPoP.
        /// </summary>
        DPoPProofKey? DPoPJsonWebKey { get; }

        /// <summary>
        /// The Client id that this token was originally requested for. 
        /// </summary>
        ClientId ClientId { get; }

        AccessTokenType? AccessTokenType { get; }
    }
}
