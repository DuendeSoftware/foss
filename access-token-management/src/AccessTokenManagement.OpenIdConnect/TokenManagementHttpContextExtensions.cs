// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.Types;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Extensions methods for HttpContext for token management
/// </summary>
public static class TokenManagementHttpContextExtensions
{
    /// <summary>
    /// Returns (and refreshes if needed) the current access token for the logged on user
    /// </summary>
    /// <param name="httpContext">The HTTP context</param>
    /// <param name="parameters">Extra optional parameters</param>
    /// <param name="cancellationToken">A cancellation token to cancel operation.</param>
    /// <returns></returns>
    public static async Task<TokenResult<UserToken>> GetUserAccessTokenAsync(
        this HttpContext httpContext,
        UserTokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var service = httpContext.RequestServices.GetRequiredService<IUserTokenManagementService>();

        return await service.GetAccessTokenAsync(httpContext.User, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Revokes the current user refresh token
    /// </summary>
    /// <param name="httpContext">The HTTP context</param>
    /// <param name="parameters">Extra optional parameters</param>
    /// <param name="cancellationToken">A cancellation token to cancel operation.</param>
    /// <returns></returns>
    public static async Task RevokeRefreshTokenAsync(
        this HttpContext httpContext,
        UserTokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var service = httpContext.RequestServices.GetRequiredService<IUserTokenManagementService>();

        await service.RevokeRefreshTokenAsync(httpContext.User, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns an access token for the OpenID Connect client using client credentials flow
    /// </summary>
    /// <param name="httpContext">The HTTP context</param>
    /// <param name="parameters">Extra optional parameters</param>
    /// <param name="cancellationToken">A cancellation token to cancel operation.</param>
    /// <returns></returns>
    public static async Task<TokenResult<ClientCredentialsToken>> GetClientAccessTokenAsync(
        this HttpContext httpContext,
        UserTokenRequestParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var service = httpContext.RequestServices.GetRequiredService<IClientCredentialsTokenManagementService>();
        var options = httpContext.RequestServices.GetRequiredService<IOptions<UserTokenManagementOptions>>();
        var schemes = httpContext.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();

        var schemeName = parameters?.ChallengeScheme ?? options.Value.ChallengeScheme;

        if (schemeName == null)
        {
            var defaultScheme = await schemes.GetDefaultChallengeSchemeAsync().ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(defaultScheme);

            schemeName = defaultScheme.Name;
        }

        return await service.GetAccessTokenAsync(
            OpenIdConnectTokenManagementDefaults.ClientCredentialsClientNamePrefix + schemeName,
            parameters,
            cancellationToken).ConfigureAwait(false);
    }

    const string AuthenticationPropertiesDPoPKey = ".Token.dpop_proof_key";
    internal static void SetProofKey(this AuthenticationProperties properties, DPoPJsonWebKey key) => properties.Items[AuthenticationPropertiesDPoPKey] = key.Value;
    internal static DPoPJsonWebKey? GetProofKey(this AuthenticationProperties properties)
    {
        if (properties.Items.TryGetValue(AuthenticationPropertiesDPoPKey, out var key))
        {
            if (key == null)
            {
                return null;
            }

            return DPoPJsonWebKey.Parse(key);
        }
        return null;
    }

    const string HttpContextDPoPKey = "dpop_proof_key";
    internal static void SetCodeExchangeDPoPKey(this HttpContext context, DPoPJsonWebKey key) => context.Items[HttpContextDPoPKey] = key;
    internal static DPoPJsonWebKey? GetCodeExchangeDPoPKey(this HttpContext context)
    {
        if (context.Items.TryGetValue(HttpContextDPoPKey, out var item))
        {
            return item as DPoPJsonWebKey;
        }
        return null;
    }
}
