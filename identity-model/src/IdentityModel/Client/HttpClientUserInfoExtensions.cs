// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Internal;

namespace Duende.IdentityModel.Client;

/// <summary>
/// HttpClient extensions for OIDC userinfo
/// </summary>
public static class HttpClientUserInfoExtensions
{
    /// <summary>
    /// Sends a userinfo request.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    public static async Task<UserInfoResponse> GetUserInfoAsync(this HttpMessageInvoker client, UserInfoRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Token.IsMissing())
        {
            throw new ArgumentNullException(nameof(request.Token));
        }

        var clone = request.Clone();

        clone.Method = HttpMethod.Get;
        clone.SetBearerToken(request.Token!);
        clone.Prepare();

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(clone, cancellationToken).ConfigureAwait();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ProtocolResponse.FromException<UserInfoResponse>(ex);
        }

        // response.Content can be null in net462 and net471
        var skipJsonParsing = response.Content?.Headers.ContentType?.MediaType != "application/json";
        return await ProtocolResponse.FromHttpResponseAsync<UserInfoResponse>(response, skipJsonParsing: skipJsonParsing).ConfigureAwait();
    }
}
