// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OTel;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Delegating handler that injects access token into an outgoing request
/// </summary>
[Obsolete(Constants.AtmPublicSurfaceInternal, UrlFormat = Constants.AtmPublicSurfaceLink)]
public abstract class AccessTokenHandler(
    AccessTokenManagementMetrics metrics,
    IDPoPProofService dPoPProofService,
    IDPoPNonceStore dPoPNonceStore,
    ILogger logger) : DelegatingHandler
{
    /// <summary>
    /// Returns the access token for the outbound call.
    /// </summary>
    /// <param name="forceRenewal"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected abstract Task<ClientCredentialsToken> GetAccessTokenAsync(bool forceRenewal, CancellationToken cancellationToken);

    protected abstract AccessTokenManagementMetrics.TokenRequestType TokenRequestType { get; }

    /// <inheritdoc/>
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "The (synchronous) Send() method is not supported. Please use the async SendAsync variant. ");

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Add a log scope that adds the Request URL to all subsequent log messages
        using var logScope = logger.BeginScope(
            (OTelParameters.RequestUrl, request.RequestUri?.GetLeftPart(UriPartial.Path))
        );

        var token = await SetTokenAsync(request, forceRenewal: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var dPoPNonce = response.GetDPoPNonce();

        // retry if 401
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            metrics.AccessTokenAccessDeniedRetry(token.ClientId, TokenRequestType);

            // if it's a DPoP nonce error, we don't need to obtain a new access token
            var force = !response.IsDPoPError();
            if (!force && !string.IsNullOrEmpty(dPoPNonce))
            {
                logger.RequestFailedWithDPoPErrorWillRetry(response.GetDPoPError());
            }
            else
            {
                logger.TokenNotAcceptedWhenSendingRequest();
            }

            await SetTokenAsync(request, forceRenewal: force, cancellationToken: cancellationToken, dpopNonce: dPoPNonce).ConfigureAwait(false);
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                logger.AccessTokenHandlerAuthenticationFailed();
                metrics.AccessTokenAuthenticationFailed(token.ClientId, TokenRequestType);

                return response;
            }

            dPoPNonce = response.GetDPoPNonce();
        }


        if (dPoPNonce == null)
        {
            return response;
        }

        var dPoPNonceContext = new DPoPNonceContext
        {
            Url = request.GetDPoPUrl(),
            Method = request.Method.ToString(),
        };
        await dPoPNonceStore.StoreNonceAsync(dPoPNonceContext, dPoPNonce, cancellationToken);

        return response;
    }

    /// <summary>
    /// Set an access token on the HTTP request
    /// </summary>
    /// <returns></returns>
    protected virtual async Task<ClientCredentialsToken> SetTokenAsync(HttpRequestMessage request,
        bool forceRenewal,
        CancellationToken cancellationToken,
        string? dpopNonce = null)
    {
        ClientCredentialsToken token;

        // ReSharper disable once ExplicitCallerInfoArgument
        using (ActivitySources.Main.StartActivity(ActivityNames.AcquiringToken))
        {
            token = await GetAccessTokenAsync(forceRenewal, cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            logger.FailedToObtainAccessTokenWhileSendingRequest();
            return token;
        }

        logger.SendAccessTokenToEndpoint(request.RequestUri?.AbsoluteUri, token.AccessTokenType);

        var scheme = token.AccessTokenType ?? OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer;

        if (!string.IsNullOrWhiteSpace(token.DPoPJsonWebKey))
        {
            // looks like this is a DPoP bound token, so try to generate the proof token
            if (!await SetDPoPProofTokenAsync(request, token, cancellationToken, dpopNonce))
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

        // checking for null AccessTokenType and falling back to "Bearer" since this might be coming
        // from an old cache/store prior to adding the AccessTokenType property.
        request.SetToken(scheme, token.AccessToken);

        return token;
    }

    /// <summary>
    /// Creates a DPoP proof token and attaches it to the request.
    /// </summary>
    protected virtual async Task<bool> SetDPoPProofTokenAsync(
        HttpRequestMessage request,
        ClientCredentialsToken token,
        CancellationToken cancellationToken,
        string? dpopNonce = null)
    {
        request.ClearDPoPProofToken();

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
            DPoPNonce = dpopNonce,
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
}
