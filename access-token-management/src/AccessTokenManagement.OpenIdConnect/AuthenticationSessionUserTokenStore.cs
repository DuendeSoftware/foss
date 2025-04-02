// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Token store using the ASP.NET Core authentication session
/// </summary>
[Obsolete(Constants.AtmPublicSurfaceInternal, UrlFormat = Constants.AtmPublicSurfaceLink)]
public class AuthenticationSessionUserAccessTokenStore(
    IHttpContextAccessor contextAccessor,
    IStoreTokensInAuthenticationProperties tokensInProps,
    ILogger<AuthenticationSessionUserAccessTokenStore> logger) : IUserTokenStore
{
    /// <inheritdoc/>
    public async Task<UserToken> GetTokenAsync(
        ClaimsPrincipal user,
        UserTokenRequestParameters? parameters = null)
    {
        parameters ??= new();
        // Resolve the cache here because it needs to have a per-request
        // lifetime. Sometimes the store itself is captured for longer than
        // that inside an HttpClient.
        var cache = GetHttpContext().RequestServices.GetRequiredService<AuthenticateResultCache>();

        // check the cache in case the cookie was re-issued via StoreTokenAsync
        // we use String.Empty as the key for a null SignInScheme
        if (!cache.TryGetValue(parameters.SignInScheme ?? string.Empty, out var result))
        {
            result = await contextAccessor.HttpContext!.AuthenticateAsync(parameters.SignInScheme).ConfigureAwait(false);
        }

        if (!result.Succeeded)
        {
            logger.CannotAuthenticateSchemeToAcquireUserAccessToken(parameters.SignInScheme ?? "default signin scheme");

            return new UserToken() { Error = "Cannot authenticate scheme" };
        }

        if (result.Properties == null)
        {
            logger.AuthenticationResultPropertiesAreNullAfterAuthenticate(parameters.SignInScheme ?? "default signin scheme");

            return new UserToken() { Error = "No properties on authentication result" };
        }

        return tokensInProps.GetUserToken(result.Properties, parameters);
    }

    private HttpContext GetHttpContext()
    {
        return contextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext should not be null!");
    }

    /// <inheritdoc/>
    public async Task StoreTokenAsync(
        ClaimsPrincipal user,
        UserToken token,
        UserTokenRequestParameters? parameters = null)
    {
        parameters ??= new();

        // Resolve the cache here because it needs to have a per-request
        // lifetime. Sometimes the store itself is captured for longer than
        // that inside an HttpClient.
        var cache = GetHttpContext().RequestServices.GetRequiredService<AuthenticateResultCache>();

        // check the cache in case the cookie was re-issued via StoreTokenAsync
        // we use String.Empty as the key for a null SignInScheme
        if (!cache.TryGetValue(parameters.SignInScheme ?? string.Empty, out var result))
        {

            result = await contextAccessor.HttpContext!.AuthenticateAsync(parameters.SignInScheme).ConfigureAwait(false);
        }

        if (result is not { Succeeded: true })
        {
            throw new Exception("Can't store tokens. User is anonymous");
        }

        // in case you want to filter certain claims before re-issuing the authentication session
        var transformedPrincipal = await FilterPrincipalAsync(result.Principal!).ConfigureAwait(false);

        await tokensInProps.SetUserToken(token, result.Properties, parameters);

        var scheme = await tokensInProps.GetSchemeAsync(parameters);

        await contextAccessor.HttpContext!.SignInAsync(scheme, transformedPrincipal, result.Properties).ConfigureAwait(false);

        // add to the cache so if GetTokenAsync is called again, we will use the updated property values
        // we use String.Empty as the key for a null SignInScheme
        cache[parameters.SignInScheme ?? string.Empty] = AuthenticateResult.Success(new AuthenticationTicket(transformedPrincipal, result.Properties, scheme));
    }

    /// <inheritdoc/>
    public Task ClearTokenAsync(
        ClaimsPrincipal user,
        UserTokenRequestParameters? parameters = null)
    {
        // don't bother here, since likely we're in the middle of signing out
        return Task.CompletedTask;
    }

    /// <summary>
    /// Allows transforming the principal before re-issuing the authentication session
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    protected virtual Task<ClaimsPrincipal> FilterPrincipalAsync(ClaimsPrincipal principal)
    {
        return Task.FromResult(principal);
    }
}
