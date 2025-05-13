// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

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
    AccessTokenRequestHandler.ITokenRetriever tokenRetriever,
    IDPopProofRequestHandler dPoPProofRequestHandler,
    ILogger<AccessTokenRequestHandler> logger)
    : DelegatingHandler
{
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "The (synchronous) Send() method is not supported. Please use the async SendAsync variant. ");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        TokenResult<IToken> tokenResult;

        // ReSharper disable once ExplicitCallerInfoArgument
        using (ActivitySources.Main.StartActivity(ActivityNames.AcquiringToken))
        {
            tokenResult = await tokenRetriever.GetToken(request, cancellationToken);
        }

        if (!tokenResult.WasSuccessful(out var token, out var failure))
        {
            logger.FailedToObtainAccessTokenWhileSendingRequest(LogLevel.Warning, failure.Error, failure.ErrorDescription);

            // Cant acquire token, so sending without
            return await base.SendAsync(request, cancellationToken);
        }

        var scheme = token.AccessTokenType?.ToScheme() ?? Scheme.Bearer;

        if (token.DPoPJsonWebKey != null)
        {
            var dpopParameters = new DPopProofRequestParameters()
            {
                AccessToken = token,
                DPoPNonce = request.GetDPoPNonce(),
                Request = request
            };

            // looks like this is a DPoP bound token, so try to generate the proof token
            if (!await dPoPProofRequestHandler.TryAcquireDPopProof(dpopParameters, cancellationToken))
            {
                // failed or opted out for this request, to fall back to Bearer 
                scheme = Scheme.Bearer;
            }
        }
        request.SetToken(scheme, token);

        // Send the actual request with the access token. 
        var httpResponseMessage = await base.SendAsync(request, cancellationToken);

        // On the response, there may be a DPOP Nonce that we need to store. 
        await dPoPProofRequestHandler.HandleDPopResponse(httpResponseMessage, cancellationToken);

        return httpResponseMessage;
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
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<TokenResult<IToken>> GetToken(
            HttpRequestMessage request,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Common information needed by access token handler
    /// </summary>
    public interface IToken
    {
        AccessTokenString AccessToken { get; }

        /// <summary>
        /// The string representation of the JSON web key to use for DPoP.
        /// </summary>
        DPoPJsonWebKey? DPoPJsonWebKey { get; }

        /// <summary>
        /// The Client id that this token was originally requested for. 
        /// </summary>
        ClientId ClientId { get; }

        AccessTokenType? AccessTokenType { get; }
    }
}
