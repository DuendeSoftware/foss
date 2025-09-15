// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.IdentityModel.DPoP;

/// <summary>
/// Shared DPoP extension methods for HTTP request/response messages
/// </summary>
public static class DPoPExtensions
{
    /// <summary>
    /// Sets the DPoP proof token request header. Clears any existing DPoP header first.
    /// </summary>
    /// <param name="request">The HTTP request message</param>
    /// <param name="proofToken">The DPoP proof token to set</param>
    public static void SetDPoPProofToken(this HttpRequestMessage request, string? proofToken)
    {
        // remove any old headers
        request.Headers.Remove(OidcConstants.HttpHeaders.DPoP);
        
        if (!string.IsNullOrEmpty(proofToken))
        {
            // set new header
            request.Headers.Add(OidcConstants.HttpHeaders.DPoP, proofToken);
        }
    }

    /// <summary>
    /// Reads the DPoP nonce header from the response
    /// </summary>
    /// <param name="response">The HTTP response message</param>
    /// <returns>The DPoP nonce value if present, otherwise null</returns>
    public static string? GetDPoPNonce(this HttpResponseMessage response) =>
        response.Headers.TryGetValues(OidcConstants.HttpHeaders.DPoPNonce, out var values)
            ? values.FirstOrDefault()
            : null;

    /// <summary>
    /// Returns the URL without any query parameters for DPoP proof generation
    /// </summary>
    /// <param name="request">The HTTP request message</param>
    /// <returns>The URL without query parameters</returns>
    public static string GetDPoPUrl(this HttpRequestMessage request) =>
        request.RequestUri!.Scheme + "://" + request.RequestUri!.Authority + request.RequestUri!.LocalPath;
}