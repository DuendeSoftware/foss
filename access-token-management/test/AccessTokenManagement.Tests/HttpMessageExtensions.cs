// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using Duende.IdentityModel;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Duende.AccessTokenManagement;

public static class HttpMessageExtensions
{

    public static void EnsureRequestUsesScheme(this HttpRequestMessage request, string expectedScheme) =>
        request.Headers.Authorization?.Scheme.ShouldBe(expectedScheme);

    public static string? GetNonce(this HttpRequestMessage request)
    {
        if (!request.Headers.TryGetValues(OidcConstants.HttpHeaders.DPoP, out var values))
        {
            return null;
        }
        var dpop = values.FirstOrDefault();
        dpop.ShouldNotBeNull();
        var deserialized = new JsonWebTokenHandler().ReadJsonWebToken(dpop);

        return deserialized.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Nonce)?.Value;
    }

    public static HttpResponseMessage WithDpopError(this HttpResponseMessage response, string newNonce)
    {
        response.StatusCode = HttpStatusCode.Unauthorized;
        return response.WithNonce(newNonce);
    }

    public static HttpResponseMessage WithNonce(this HttpResponseMessage response, string newNonce)
    {
        response.Headers.Add("DPoP-Nonce", newNonce);
        return response;
    }


    public static async Task<HttpResponseMessage> CheckHttpStatusCode(this Task<HttpResponseMessage> getResponse,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var response = await getResponse;
        if (response.StatusCode != statusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Expected {statusCode} but got {response.StatusCode}. Content: {content}");
        }

        return response;
    }

    public static async Task<HttpResponseMessage> CheckResponseContent(this Task<HttpResponseMessage> getResponse,
        string value)
    {
        var response = await getResponse;
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldBe(value);

        return response;
    }

}
