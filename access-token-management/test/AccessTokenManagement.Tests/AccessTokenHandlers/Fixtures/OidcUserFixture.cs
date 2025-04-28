// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.AccessTokenHandlers.Helpers;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AccessTokenManagement.AccessTokenHandlers.Fixtures;

internal class OidcUserFixture : AccessTokenHandlingBaseFixture
{
    public override async ValueTask InitializeAsync(bool usePreviewFeature, string? dPoPJsonWebKey)
    {
        ApiEndpoint.DefaultRespondOkWithToken();
        TokenEndpoint.DefaultRespondWithAccessToken();
        Services.AddSingleton(new TestAccessTokens(dPoPJsonWebKey));
        Services.AddSingleton<FakeAuthenticationService>();
        Services.AddSingleton<IAuthenticationService>(sp => sp.GetRequiredService<FakeAuthenticationService>());

        Services.AddAuthentication()
            .AddOpenIdConnect(opt =>
            {
                opt.ClientId = "clientId";
                opt.ClientSecret = "clientSecret";
                opt.Authority = TokenEndpoint.Uri.ToString();
                opt.BackchannelHttpHandler = TokenEndpoint;
            });
        Services.AddSingleton<IHttpContextAccessor>(sp =>
        {
            var httpContextAccessor = new FakeHttpContextAccessor
            {
                HttpContext = new DefaultHttpContext()
                {
                    User = sp.GetRequiredService<FakeAuthenticationService>().Principal,
                    RequestServices = sp
                }
            };
            return httpContextAccessor;
        });

        Services.AddOpenIdConnectAccessTokenManagement(opt =>
        {
            opt.UsePreviewExtensibilityOnAccessTokenHandlers = usePreviewFeature;
            opt.DPoPJsonWebKey = dPoPJsonWebKey;
        });

        Services.AddDistributedMemoryCache();

        Services.AddClientAccessTokenHttpClient("httpClient", new UserTokenRequestParameters()
        {

        })
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = ApiEndpoint.Uri;
            })
            .ConfigurePrimaryHttpMessageHandler(() => ApiEndpoint);

        await TokenEndpoint.SetupDiscoveryDocuments();
    }
}
