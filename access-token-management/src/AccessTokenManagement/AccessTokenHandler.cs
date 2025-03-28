// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Microsoft.Extensions.Logging;
using static Duende.IdentityModel.OidcConstants;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Delegating handler that injects access token into an outgoing request
/// </summary>
public abstract class AccessTokenHandler(
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

    /// <inheritdoc/>
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new NotSupportedException(
            "The (synchronous) Send() method is not supported. Please use the async SendAsync variant. ");
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Add a log scope that adds the Request URL to all subsequent log messages
        using var logScope = logger.BeginScope(
            (LogMessages.Parameters.RequestUrl, request.RequestUri?.GetLeftPart(UriPartial.Path))
        );

        await SetTokenAsync(request, forceRenewal: false, cancellationToken).ConfigureAwait(false);
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var dPoPNonce = response.GetDPoPNonce();

        // retry if 401
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            response.Dispose();

            // if it's a DPoP nonce error, we don't need to obtain a new access token
            var force = !response.IsDPoPError();
            if (!force && !string.IsNullOrEmpty(dPoPNonce))
            {
                logger.RequestFailedWithDPoPErrorWillRetry(response?.GetDPoPError(), request.RequestUri?.AbsoluteUri);
            }

            await SetTokenAsync(request, forceRenewal: force, cancellationToken, dPoPNonce).ConfigureAwait(false);
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        else if (dPoPNonce != null)
        {
            await dPoPNonceStore.StoreNonceAsync(new DPoPNonceContext
            {
                Url = request.GetDPoPUrl(),
                Method = request.Method.ToString(),
            }, dPoPNonce, cancellationToken);
        }

        return response;
    }

    /// <summary>
    /// Set an access token on the HTTP request
    /// </summary>
    /// <returns></returns>
    protected virtual async Task SetTokenAsync(HttpRequestMessage request, bool forceRenewal, CancellationToken cancellationToken, string? dpopNonce = null)
    {
        var token = await GetAccessTokenAsync(forceRenewal, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(token?.AccessToken))
        {
            logger.SendAccessTokenToEndpoint(request.RequestUri?.AbsoluteUri, token.AccessTokenType);

            var scheme = token.AccessTokenType ?? AuthenticationSchemes.AuthorizationHeaderBearer;

            if (!string.IsNullOrWhiteSpace(token.DPoPJsonWebKey))
            {
                // looks like this is a DPoP bound token, so try to generate the proof token
                if (!await SetDPoPProofTokenAsync(request, token, cancellationToken, dpopNonce))
                {
                    // failed or opted out for this request, to fall back to Bearer 
                    scheme = AuthenticationSchemes.AuthorizationHeaderBearer;
                }
            }

            // since AccessTokenType above in the token endpoint response (the token_type value) could be case insensitive, but
            // when we send it as an Authorization header in the API request it must be case sensitive, we 
            // are checking for that here and forcing it to the exact casing required.
            if (scheme.Equals(AuthenticationSchemes.AuthorizationHeaderBearer, System.StringComparison.OrdinalIgnoreCase))
            {
                scheme = AuthenticationSchemes.AuthorizationHeaderBearer;
            }
            else if (scheme.Equals(AuthenticationSchemes.AuthorizationHeaderDPoP, System.StringComparison.OrdinalIgnoreCase))
            {
                scheme = AuthenticationSchemes.AuthorizationHeaderDPoP;
            }

            // checking for null AccessTokenType and falling back to "Bearer" since this might be coming
            // from an old cache/store prior to adding the AccessTokenType property.
            request.SetToken(scheme, token.AccessToken);
        }
    }

    /// <summary>
    /// Creates a DPoP proof token and attaches it to the request.
    /// </summary>
    protected virtual async Task<bool> SetDPoPProofTokenAsync(HttpRequestMessage request, ClientCredentialsToken token, CancellationToken cancellationToken, string? dpopNonce = null)
    {
        // remove any old headers
        request.ClearDPoPProofToken();

        if (!string.IsNullOrEmpty(token.DPoPJsonWebKey))
        {
            request.TryGetDPopProofAdditionalPayloadClaims(out var additionalClaims);

            // create proof
            var proofToken = await dPoPProofService.CreateProofTokenAsync(new DPoPProofRequest
            {
                AccessToken = token.AccessToken,
                Url = request.GetDPoPUrl(),
                Method = request.Method.ToString(),
                DPoPJsonWebKey = token.DPoPJsonWebKey,
                DPoPNonce = dpopNonce,
                AdditionalPayloadClaims = additionalClaims,
            });

            if (proofToken != null)
            {
                logger.SendingDPoPProofToken(request.RequestUri?.AbsoluteUri);

                request.SetDPoPProofToken(proofToken.ProofToken);
                return true;
            }
            else
            {
                logger.FailedToCreateDPopProofToken(request.RequestUri?.AbsoluteUri);
            }
        }

        return false;
    }
}
