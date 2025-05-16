// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;

namespace Duende.AccessTokenManagement.Internal;
internal static class RequestExtensions
{
    private static readonly HttpRequestOptionsKey<ClientId> ClientIdOptionsKey = new("Duende.AccessTokenManagement.ClientId");

    /// <summary>
    /// Set the token to a http request. 
    /// </summary>
    /// <param name="request">The http request</param>
    /// <param name="scheme">The scheme to use. </param>
    /// <param name="token">The token to set</param>
    internal static void SetToken(this HttpRequestMessage request, Scheme scheme, AccessTokenRequestHandler.IToken token)
    {
        // Set the client ID on the request so down the line we know what client ID this token was issued for
        request.Options.Set(ClientIdOptionsKey, token.ClientId);
        request.SetToken(scheme.ToString(), token.AccessToken.ToString());
    }

    /// <summary>
    /// Retrieve the client id, that was previously set using <see cref="SetToken"/>
    /// </summary>
    /// <param name="request">The request</param>
    /// <returns>If present, the client id</returns>
    internal static ClientId? GetClientId(this HttpRequestMessage request)
    {
        request.Options.TryGetValue(ClientIdOptionsKey, out var clientId);
        return clientId;
    }
}
