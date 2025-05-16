// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Duende.AccessTokenManagement.DPoP;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Extensions methods for HttpContext for token management
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Returns (and refreshes if needed) the current access token for the logged on user
    /// </summary>
    /// <param name="httpContext">The HTTP context</param>
    /// <param name="parameters">Extra optional parameters</param>
    /// <param name="ct">A cancellation token to cancel operation.</param>
    /// <returns></returns>
    public static async Task<TokenResult<UserToken>> GetUserAccessTokenAsync(
        this HttpContext httpContext,
        UserTokenRequestParameters? parameters = null,
        CT ct = default)
    {
        var service = httpContext.RequestServices.GetRequiredService<IUserTokenManager>();

        return await service.GetAccessTokenAsync(httpContext.User, parameters, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Revokes the current user refresh token
    /// </summary>
    /// <param name="httpContext">The HTTP context</param>
    /// <param name="parameters">Extra optional parameters</param>
    /// <param name="ct">A cancellation token to cancel operation.</param>
    /// <returns></returns>
    public static async Task RevokeRefreshTokenAsync(
        this HttpContext httpContext,
        UserTokenRequestParameters? parameters = null,
        CT ct = default)
    {
        var service = httpContext.RequestServices.GetRequiredService<IUserTokenManager>();

        await service.RevokeRefreshTokenAsync(httpContext.User, parameters, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns an access token for the OpenID Connect client using client credentials flow
    /// </summary>
    /// <param name="httpContext">The HTTP context</param>
    /// <param name="parameters">Extra optional parameters</param>
    /// <param name="ct">A cancellation token to cancel operation.</param>
    /// <returns></returns>
    public static async Task<TokenResult<ClientCredentialsToken>> GetClientAccessTokenAsync(
        this HttpContext httpContext,
        UserTokenRequestParameters? parameters = null,
        CT ct = default)
    {
        var service = httpContext.RequestServices.GetRequiredService<IClientCredentialsTokenManager>();
        var options = httpContext.RequestServices.GetRequiredService<IOptions<UserTokenManagementOptions>>();
        var schemes = httpContext.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();

        var schemeName = parameters?.ChallengeScheme ?? options.Value.ChallengeScheme;

        if (schemeName == null)
        {
            var defaultScheme = await schemes.GetDefaultChallengeSchemeAsync().ConfigureAwait(false);
            if (defaultScheme == null)
            {
                throw new InvalidOperationException("Cannot retrieve client access token. No scheme was provided and default challenge scheme was not set.");
            }

            schemeName = defaultScheme.Name;
        }

        return await service.GetAccessTokenAsync(
            OpenIdConnectTokenManagementDefaults.ClientCredentialsClientNamePrefix + schemeName,
            parameters,
            ct).ConfigureAwait(false);
    }

    const string AuthenticationPropertiesDPoPKey = ".Token.dpop_proof_key";
    internal static void SetProofKey(this AuthenticationProperties properties, ProofKeyString keyString) => properties.Items[AuthenticationPropertiesDPoPKey] = keyString.ToString();
    internal static ProofKeyString? GetProofKey(this AuthenticationProperties properties)
    {
        if (properties.Items.TryGetValue(AuthenticationPropertiesDPoPKey, out var key))
        {
            if (key == null)
            {
                return null;
            }

            return ProofKeyString.Parse(key);
        }
        return null;
    }

    const string HttpContextDPoPKey = "dpop_proof_key";
    internal static void SetCodeExchangeDPoPKey(this HttpContext context, ProofKeyString keyString) => context.Items[HttpContextDPoPKey] = keyString;
    internal static ProofKeyString? GetCodeExchangeDPoPKey(this HttpContext context)
    {
        if (context.Items.TryGetValue(HttpContextDPoPKey, out var item))
        {
            return item as ProofKeyString?;
        }
        return null;
    }
}
