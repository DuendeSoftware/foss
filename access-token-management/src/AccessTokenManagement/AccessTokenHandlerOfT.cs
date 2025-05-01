// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OTel;
using Duende.IdentityModel;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement;

/// <summary>
/// This HttpMessageHandler is used to acquire and add an access token while sending
/// http requests. The method on how to aquire tokens, the retry policy and the dpop handling
/// can be customized. 
/// </summary>
public sealed class AccessTokenRequestHandler : DelegatingHandler
{
    private ITokenRetriever _tokenRetriever;
    private readonly ISendRequestRetryHandler _retryHandler;
    private readonly IDPopProofRequestHandler _dPoPProofRequestHandler;
    private readonly ILogger<AccessTokenRequestHandler> _logger;

    internal AccessTokenRequestHandler(
        ISendRequestRetryHandler retryHandler,
        ITokenRetriever tokenRetriever,
        IDPopProofRequestHandler dPoPProofRequestHandler,
        ILogger<AccessTokenRequestHandler> logger)
    {
        _retryHandler = retryHandler;
        _dPoPProofRequestHandler = dPoPProofRequestHandler;
        _logger = logger;
        _tokenRetriever = tokenRetriever;
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "The (synchronous) Send() method is not supported. Please use the async SendAsync variant. ");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await _retryHandler.Handle(request, SendRequestWithToken, cancellationToken);

        return response;
    }

    private async Task<HttpResponseMessage> SendRequestWithToken(
        AccessTokenHandlerRequestParameters parameters,
        CancellationToken cancellationToken)
    {
        var request = parameters.Request;
        // Add a log scope that adds the Request URL to all subsequent log messages
        using var logScope = _logger.BeginScope(
            (OTelParameters.RequestUrl, request.RequestUri?.GetLeftPart(UriPartial.Path))
        );

        var token = await _tokenRetriever.GetToken(request, parameters.ForceTokenRefresh, cancellationToken);

        var scheme = token.AccessTokenType ?? OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer;

        if (!string.IsNullOrWhiteSpace(token.DPoPJsonWebKey))
        {
            var dpopParameters = new DPopProofRequestParameters()
            {
                ClientCredentialsToken = token,
                DPoPNonce = parameters.DPoPNonce,
                Request = request
            };

            // looks like this is a DPoP bound token, so try to generate the proof token
            if (!await _dPoPProofRequestHandler.TryAcquireDPopProof(dpopParameters, cancellationToken))
            {
                // failed or opted out for this request, to fall back to Bearer 
                scheme = OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer;
            }
        }

        // since AccessTokenType above in the token endpoint response (the token_type value) could be case insensitive, but
        // when we send it as an Authorization header in the API request it must be case sensitive, we 
        // are checking for that here and forcing it to the exact casing required.
        if (scheme.Equals(OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer, StringComparison.OrdinalIgnoreCase))
        {
            scheme = OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer;
        }
        else if (scheme.Equals(OidcConstants.AuthenticationSchemes.AuthorizationHeaderDPoP, StringComparison.OrdinalIgnoreCase))
        {
            scheme = OidcConstants.AuthenticationSchemes.AuthorizationHeaderDPoP;
        }

        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            _logger.FailedToObtainAccessTokenWhileSendingRequest();
        }
        else
        {
            // checking for null AccessTokenType and falling back to "Bearer" since this might be coming
            // from an old cache/store prior to adding the AccessTokenType property.
            request.SetToken(scheme, token);
        }

        // Send the actual request with the access token. 
        var httpResponseMessage = await base.SendAsync(request, cancellationToken);

        // On the response, there may be a DPOP Nonce that we need to store. 
        await _dPoPProofRequestHandler.HandleDPopResponse(httpResponseMessage, cancellationToken);

        return httpResponseMessage;
    }
}
