// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

public static class HttpRequestMessageExtensions
{
    public static void SetToken(this HttpRequestMessage request, string scheme, ClientCredentialsToken token)
    {
        request.Options.Set(new HttpRequestOptionsKey<string>("Duende.AccessTokenManagement.ClientId"), token.ClientId ?? "unknown_client");

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(scheme, token.AccessToken);
    }

    public static string GetClientId(this HttpResponseMessage response)
    {
        var request = response.RequestMessage;

        if (request == null)
        {
            return "unknown_client";
        }

        if (request.Options.TryGetValue(new HttpRequestOptionsKey<string>("Duende.AccessTokenManagement.ClientId"), out var clientId))
        {
            return clientId;
        }
        return "unknown_client";
    }

}
