// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Duende.AccessTokenManagement.DPoP;
using Duende.IdentityModel.Client;

namespace Duende.AccessTokenManagement.Framework;

public class TestDataBuilder(TestData The)
{

    public Token Token() => new()
    {
        access_token = The.AccessToken.ToString(),
        token_type = The.TokenType.ToString(),
        scope = The.Scope.ToString(),
        expires_in = null// by default, don't set the expires value
    };

    public HttpResponseMessage TokenHttpResponse(object? tokenResponse = null) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(tokenResponse ?? Token(), mediaType: MediaTypeHeaderValue.Parse("application/json"))
        };



    public void ClientCredentialsClient(
        ClientCredentialsClient toConfigure,
        DPoPProofKey? jsonWebKey = null,
        Resource? resource = null,
        ClientCredentialStyle? style = null,
        Dictionary<string, string>? parameters = null)
    {
        toConfigure.ClientSecret = The.ClientSecret;
        toConfigure.ClientId = The.ClientId;
        toConfigure.TokenEndpoint = The.TokenEndpoint;
        toConfigure.DPoPJsonWebKey = jsonWebKey;
        toConfigure.Scope = The.Scope;
        toConfigure.Resource = resource;
        if (style != null)
        {
            toConfigure.ClientCredentialStyle = style.Value;
        }

        foreach (var parameter in parameters ?? [])
        {
            toConfigure.Parameters.Add(parameter);
        }
    }

    public ClientCredentialsToken ClientCredentialsToken() => new()
    {
        AccessToken = The.AccessToken,
        AccessTokenType = The.TokenType,
        DPoPJsonWebKey = null,
        Expiration = DateTimeOffset.MaxValue,
        Scope = The.Scope,
        ClientId = The.ClientId
    };

}

public record Token
{
    public string? access_token { get; init; }
    public string? token_type { get; init; }
    public string? scope { get; init; }
    public int? expires_in { get; init; }
}
