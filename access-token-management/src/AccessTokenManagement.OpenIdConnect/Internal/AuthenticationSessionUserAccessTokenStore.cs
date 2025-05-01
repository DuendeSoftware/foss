// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.AccessTokenManagement.Internal;
using Duende.AccessTokenManagement.Types;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

/// <summary>
/// Allows transforming the principal before re-issuing the authentication session
/// </summary>
/// <param name="principal"></param>
/// <returns></returns>

public delegate Task<ClaimsPrincipal> TransformPrincipalAsync(ClaimsPrincipal principal, CancellationToken ct);

/// <summary>
/// Token store using the ASP.NET Core authentication session
/// </summary>
internal class AuthenticationSessionUserAccessTokenStore(
    IHttpContextAccessor contextAccessor,
    IStoreTokensInAuthenticationProperties tokensInProps,
    ILogger<AuthenticationSessionUserAccessTokenStore> logger,
    TransformPrincipalAsync? principalTransformer = null) : IUserTokenStore
{
    /// <inheritdoc/>
    public async Task<TokenResult<TokenForParameters>> GetTokenAsync(
        ClaimsPrincipal user,
        UserTokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        parameters ??= new();
        // Resolve the cache here because it needs to have a per-request
        // lifetime. Sometimes the store itself is captured for longer than
        // that inside an HttpClient.
        var cache = GetHttpContext().RequestServices.GetRequiredService<AuthenticateResultCache>();

        // check the cache in case the cookie was re-issued via StoreTokenAsync
        // we use String.Empty as the key for a null SignInScheme
        if (!cache.TryGetValue(parameters.SignInScheme, out var result))
        {
            result = await contextAccessor.HttpContext!.AuthenticateAsync(parameters.SignInScheme?.Value)
                .ConfigureAwait(false);
        }

        if (!result.Succeeded)
        {
            logger.CannotAuthenticateSchemeToAcquireUserAccessToken(parameters.SignInScheme);

            return new FailedResult("Cannot authenticate scheme");
        }

        if (result.Properties == null)
        {
            logger.AuthenticationResultPropertiesAreNullAfterAuthenticate(parameters.SignInScheme);

            return new FailedResult("Cannot authenticate scheme");
        }

        return tokensInProps.GetUserToken(result.Properties, parameters);
    }

    private HttpContext GetHttpContext() => contextAccessor.HttpContext ??
                                            throw new InvalidOperationException("HttpContext should not be null!");

    /// <inheritdoc/>
    public async Task StoreTokenAsync(
        ClaimsPrincipal user,
        UserToken token,
        UserTokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        parameters ??= new();

        // Resolve the cache here because it needs to have a per-request
        // lifetime. Sometimes the store itself is captured for longer than
        // that inside an HttpClient.
        var cache = GetHttpContext().RequestServices.GetRequiredService<AuthenticateResultCache>();

        // check the cache in case the cookie was re-issued via StoreTokenAsync
        // we use String.Empty as the key for a null SignInScheme
        if (!cache.TryGetValue(parameters.SignInScheme, out var result))
        {
            result = await contextAccessor.HttpContext!
                .AuthenticateAsync(parameters.SignInScheme?.Value)
                .ConfigureAwait(false);
        }

        if (result is not { Succeeded: true })
        {
            throw new InvalidOperationException("Can't store tokens. User is anonymous.");
        }

        var principal = result.Principal ??
                        throw new InvalidOperationException("Principal was null after authentication");

        if (principalTransformer != null)
        {
            principal = await principalTransformer(principal, CancellationToken.None).ConfigureAwait(false);
        }

        await tokensInProps.SetUserToken(token, result.Properties, parameters, cancellationToken);

        var scheme = await tokensInProps.GetSchemeAsync(parameters, cancellationToken);

        await contextAccessor.HttpContext!.SignInAsync(scheme.Value, principal, result.Properties)
            .ConfigureAwait(false);

        // add to the cache so if GetTokenAsync is called again, we will use the updated property values
        cache[parameters.SignInScheme] =
            AuthenticateResult.Success(new AuthenticationTicket(principal, result.Properties, scheme.Value));
    }

    /// <inheritdoc/>
    // don't bother here, since likely we're in the middle of signing out
    public Task ClearTokenAsync(ClaimsPrincipal user, UserTokenRequestParameters? parameters = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

