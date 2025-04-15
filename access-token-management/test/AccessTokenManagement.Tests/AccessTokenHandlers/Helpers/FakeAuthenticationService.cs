// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Duende.AccessTokenManagement.AccessTokenHandlers.Helpers;

public class FakeAuthenticationService(IStoreTokensInAuthenticationProperties storeTokens, TestAccessTokens testTokens) : IAuthenticationService
{
    public ClaimsPrincipal Principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "sub")], "test"));

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
        Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(Principal, BuildProperties(), "oidc")));

    private AuthenticationProperties? BuildProperties()
    {
        var properties = new AuthenticationProperties();

#pragma warning disable CS0618 // Type or member is obsolete
        storeTokens.SetUserToken(testTokens.UserToken, properties);
#pragma warning restore CS0618 // Type or member is obsolete

        return properties;
    }

    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;

    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
}
