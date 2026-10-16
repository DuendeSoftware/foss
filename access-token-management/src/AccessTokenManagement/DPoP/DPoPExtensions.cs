// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.IdentityModel;

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Extensions for HTTP request/response messages
/// </summary>
public static class DPoPExtensions
{
    private static readonly HttpRequestOptionsKey<bool> ForceRenewalOptionsKey = new("Duende.AccessTokenManagement.ForceRenewal");
    private static readonly HttpRequestOptionsKey<DPoPNonce> DPoPNonceOptionsKey = new("Duende.AccessTokenManagement.DPoPNonce");

    /// <summary>
    /// Sets whether this request should force token renewal.
    /// </summary>
    /// <param name="request">The request to annotate.</param>
    /// <param name="forceTokenRenewal"><see langword="true"/> to force renewal; otherwise <see langword="false"/>.</param>
    public static void SetForceRenewal(this HttpRequestMessage request, bool forceTokenRenewal) => request.Options.Set(ForceRenewalOptionsKey, forceTokenRenewal);

    /// <summary>
    /// Gets whether this request is marked to force token renewal.
    /// </summary>
    /// <param name="request">The request to inspect.</param>
    /// <returns><see langword="true"/> if force-renewal is enabled; otherwise <see langword="false"/>.</returns>
    public static bool GetForceRenewal(this HttpRequestMessage request)
    {
        if (request.Options.TryGetValue(ForceRenewalOptionsKey, out var forceRenewal))
        {
            return forceRenewal;
        }
        return false;
    }

    /// <summary>
    /// Gets the DPoP nonce associated with this request, if one was set.
    /// </summary>
    /// <param name="request">The request to inspect.</param>
    /// <returns>The nonce value, or <see langword="null"/> when none is present.</returns>
    public static DPoPNonce? GetDPoPNonce(this HttpRequestMessage request)
    {
        if (request.Options.TryGetValue(DPoPNonceOptionsKey, out var nonce))
        {
            return nonce;
        }
        return null;
    }

    /// <summary>
    /// Sets the DPoP nonce to use when generating a proof for this request.
    /// </summary>
    /// <param name="request">The request to annotate.</param>
    /// <param name="nonce">The nonce value.</param>
    public static void SetDPoPNonce(this HttpRequestMessage request, DPoPNonce nonce) => request.Options.Set(DPoPNonceOptionsKey, nonce);

    /// <summary>
    /// Clears any existing DPoP nonce headers.
    /// </summary>
    public static void ClearDPoPProofToken(this HttpRequestMessage request) =>
        // remove any old headers
        request.Headers.Remove(OidcConstants.HttpHeaders.DPoP);

    /// <summary>
    /// Sets the DPoP nonce request header if nonce is not null.
    /// </summary>
    public static void SetDPoPProofToken(this HttpRequestMessage request, DPoPProof proof) =>
        // set new header
        request.Headers.Add(OidcConstants.HttpHeaders.DPoP, proof.ToString());

    /// <summary>
    /// Reads the DPoP nonce header from the response
    /// </summary>
    public static DPoPNonce? GetDPoPNonce(this HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(OidcConstants.HttpHeaders.DPoPNonce, out var values))
        {
            return null;
        }

        return DPoPNonce.ParseOrDefault(values.FirstOrDefault());
    }

    /// <summary>
    /// Reads the DPoP error from the response
    /// </summary>
    public static string? GetDPoPError(this HttpResponseMessage response)
    {
        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
        {
            return null;
        }

        var header = response.Headers.WwwAuthenticate.FirstOrDefault(
            x => x.Scheme == OidcConstants.AuthenticationSchemes.AuthorizationHeaderDPoP);
        if (header?.Parameter == null)
        {
            return null;
        }

        // WWW-Authenticate: DPoP error="use_dpop_nonce"
        var values = header.Parameter.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var error = values
            .Select(x =>
            {
                var parts = x.Split('=', StringSplitOptions.RemoveEmptyEntries);
                if (parts is [OidcConstants.TokenResponse.Error, _])
                {
                    return parts[1].Trim('"');
                }
                return null;
            })
            .FirstOrDefault(x => x != null);

        return error;
    }

    /// <summary>
    /// Checks if the DPoP error matches specific errors
    /// </summary>
    public static bool IsDPoPError(this HttpResponseMessage response)
    {
        var error = response.GetDPoPError();
        return DPoPErrors.IsDPoPError(error);
    }

    /// <summary>
    /// Returns the URL without any query params
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public static Uri GetDPoPUrl(this HttpRequestMessage request) =>
        new(request.RequestUri!.Scheme + "://" + request.RequestUri!.Authority + request.RequestUri!.LocalPath);

    /// <summary>
    /// Additional claims that will be added to the DPoP proof payload on generation
    /// </summary>
    /// <param name="request"></param>
    /// <param name="customClaims"></param>
    public static void AddDPoPProofAdditionalPayloadClaims(
        this HttpRequestMessage request,
        IDictionary<string, string> customClaims) => request.Options.TryAdd(
            ClientCredentialsTokenManagementDefaults.DPoPProofAdditionalPayloadClaims,
            customClaims.AsReadOnly());

    /// <summary>
    /// Additional claims that will be added to the DPoP proof payload on generation
    /// </summary>
    /// <param name="request"></param>
    /// <param name="additionalClaims"></param>
    /// <returns></returns>
    public static bool TryGetDPopProofAdditionalPayloadClaims(
        this HttpRequestMessage request,
        [NotNullWhen(true)] out IReadOnlyDictionary<string, string>? additionalClaims)
    {
        var key = new HttpRequestOptionsKey<IReadOnlyDictionary<string, string>>(
            ClientCredentialsTokenManagementDefaults.DPoPProofAdditionalPayloadClaims);

        return request.Options.TryGetValue(key, out additionalClaims);
    }
}
