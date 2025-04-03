// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net.Http.Headers;
using Duende.IdentityModel.Internal;

namespace Duende.IdentityModel.Client;

/// <summary>
/// HttpClient extensions for OAuth token introspection
/// </summary>
public static class HttpClientTokenIntrospectionExtensions
{
    /// <summary>
    /// Sends an OAuth token introspection request.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    public static async Task<TokenIntrospectionResponse> IntrospectTokenAsync(this HttpMessageInvoker client, TokenIntrospectionRequest request, CancellationToken cancellationToken = default)
    {
        var clone = request.Clone();

        clone.Method = HttpMethod.Post;
        clone.Parameters.AddRequired(OidcConstants.TokenIntrospectionRequest.Token, request.Token);
        clone.Parameters.AddOptional(OidcConstants.TokenIntrospectionRequest.TokenTypeHint, request.TokenTypeHint);
        clone.Prepare();

        HttpResponseMessage response;
        try
        {
            if (request.ResponseFormat is IntrospectionResponseFormat.Jwt)
            {
                clone.Headers.Accept.Clear();
                clone.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue($"application/{JwtClaimTypes.JwtTypes.IntrospectionJwtResponse}"));
            }

            response = await client.SendAsync(clone, cancellationToken).ConfigureAwait();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ProtocolResponse.FromException<TokenIntrospectionResponse>(ex);
        }

        return await ProtocolResponse.FromHttpResponseAsync<TokenIntrospectionResponse>(response).ConfigureAwait();
    }
}
