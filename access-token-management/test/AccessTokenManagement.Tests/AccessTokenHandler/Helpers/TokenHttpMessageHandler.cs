// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using Duende.AccessTokenManagement.Tests;
using Duende.IdentityModel;
using RichardSzalay.MockHttp;

namespace Duende.AccessTokenManagement.AccessTokenHandlers.Helpers;

public class TokenHttpMessageHandler : MockHttpMessageHandler, IAsyncDisposable
{
    public Uri Uri = new Uri("https://idp");
    public Uri TokenEndpoint = new Uri("https://idp/connect/token");

    public int TokenSeed = 1;
    private IdentityServerHost? _host;

    public async Task SetupDiscoveryDocuments()
    {
        // We need a way to serve 'valid' discovery documents. The easiest I could think of
        // is to serve them from an actual identity server
        _host = new IdentityServerHost(_ => { }, Uri.ToString());
        await _host.InitializeAsync();
        this.When(new Uri(Uri, ".well-known/openid-configuration").ToString()).Respond(_host.HttpClient);
        this.When(new Uri(Uri, ".well-known/openid-configuration/jwks").ToString()).Respond(_host.HttpClient);
    }


    public void RespondWithTokenType(string tokenType) => this.Expect(HttpMethod.Post, TokenEndpoint.ToString())
            .Respond(request =>
            {
                var initialTokenResponse = BuildAccessToken(tokenType: tokenType);

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(initialTokenResponse)
                });
            });

    public void DefaultRespondWithAccessToken() => this.When(HttpMethod.Post, TokenEndpoint.ToString())
            .Respond(request =>
            {
                var initialTokenResponse = BuildAccessToken();

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(initialTokenResponse)
                });
            });

    private object BuildAccessToken(int? expireInSeconds = 3600, string tokenType = OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer)
    {
        var initialTokenResponse = new
        {
            id_token = "id_token",
            access_token = "access_token_" + TokenSeed++,
            token_type = tokenType,
            expires_in = expireInSeconds,
            refresh_token = "refresh_token",
        };
        return initialTokenResponse;
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_host != null)
        {
            await _host.DisposeAsync();
        }
        Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }
}
