// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Globalization;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.Internal;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

/// <inheritdoc/>
internal class StoreTokensInAuthenticationProperties(
    IOptionsMonitor<UserTokenManagementOptions> tokenManagementOptionsMonitor,
    IOptionsMonitor<CookieAuthenticationOptions> cookieOptionsMonitor,
    IAuthenticationSchemeProvider schemeProvider,
    ILogger<StoreTokensInAuthenticationProperties> logger
) : IStoreTokensInAuthenticationProperties
{
    private const string TokenPrefix = ".Token.";
    private const string TokenNamesKey = ".TokenNames";
    private const string DPoPKeyName = "dpop_proof_key";

    /// Adds the .Token. prefix to the token name and, if the resource
    /// parameter was included, the suffix marking this token as
    /// per-resource.
    private static string NamePrefixAndResourceSuffix(string type, UserTokenRequestParameters? parameters)
    {
        var result = NamePrefix(type);
        if (parameters?.Resource != null)
        {
            result = ResourceSuffix(result, parameters.Resource.Value);
        }
        return result;
    }

    private static string NamePrefix(string name) => $"{TokenPrefix}{name}";

    private static string ResourceSuffix(string name, Resource resource) => $"{name}::{resource}";

    private static string ChallengeSuffix(string name, Scheme challengeScheme) => $"{name}||{challengeScheme}";

    /// <inheritdoc/>
    public TokenResult<TokenForParameters> GetUserToken(AuthenticationProperties authenticationProperties, UserTokenRequestParameters? parameters = null)
    {
        var tokens = authenticationProperties.Items.Where(i => i.Key.StartsWith(TokenPrefix)).ToList();
        if (!tokens.Any())
        {
            logger.FailedToGetUserTokenDueToMissingTokensInCookie(LogLevel.Warning);

            return new FailedResult("No tokens in properties");
        }

        var names = GetTokenNamesWithoutScheme(parameters);

        var appendChallengeScheme = AppendChallengeSchemeToTokenNames(parameters);

        var accessTokenValue = GetTokenValue(tokens, names.Token, appendChallengeScheme, parameters);
        var clientId = GetTokenValue(tokens, names.ClientId, appendChallengeScheme, parameters);
        var accessTokenType = GetTokenValue(tokens, names.TokenType, appendChallengeScheme, parameters);
        var dpopKey = GetTokenValue(tokens, names.DPoPKey, appendChallengeScheme, parameters);
        var expiresAt = GetTokenValue(tokens, names.Expires, appendChallengeScheme, parameters);
        var refreshTokenValue = GetTokenValue(tokens, names.RefreshToken, appendChallengeScheme, parameters);
        var identityTokenValue = GetTokenValue(tokens, names.IdentityToken, appendChallengeScheme, parameters);

        var dtExpires = DateTimeOffset.MaxValue;
        if (expiresAt != null)
        {
            dtExpires = DateTimeOffset.Parse(expiresAt, CultureInfo.InvariantCulture);
        }

        if (accessTokenValue == null && refreshTokenValue == null)
        {
            return new FailedResult("No AccessToken or RefreshToken present in properties");
        }

        var refreshToken = refreshTokenValue == null
            ? null
            : new UserRefreshToken(
                RefreshToken.Parse(refreshTokenValue),
                DPoPProofKey.ParseOrDefault(dpopKey));

        if (accessTokenValue == null && refreshToken != null)
        {
            return new TokenForParameters(refreshToken);
        }

        var userToken = new UserToken
        {
            AccessToken = AccessToken.Parse(accessTokenValue ?? throw new NullReferenceException("access_token should not be null here.")),
            AccessTokenType = AccessTokenType.ParseOrDefault(accessTokenType),
            DPoPJsonWebKey = DPoPProofKey.ParseOrDefault(dpopKey),
            RefreshToken = refreshToken?.RefreshToken,
            Expiration = dtExpires,
            ClientId = ClientId.Parse(clientId ?? "unknown"),
            IdentityToken = IdentityToken.ParseOrDefault(identityTokenValue),
        };
        return new TokenForParameters(userToken, refreshToken);
    }

    /// <inheritdoc/>
    public async Task SetUserTokenAsync(
        UserToken token,
        AuthenticationProperties authenticationProperties,
        UserTokenRequestParameters? parameters = null,
        CT ct = default)
    {
        var tokenNames = GetTokenNamesWithScheme(parameters);

        authenticationProperties.Items[tokenNames.Token] = token.AccessToken.ToString();
        authenticationProperties.Items[tokenNames.ClientId] = token.ClientId.ToString();
        authenticationProperties.Items[tokenNames.IdentityToken] = token.IdentityToken?.ToString();
        authenticationProperties.Items[tokenNames.TokenType] = token.AccessTokenType?.ToString();
        if (token.DPoPJsonWebKey != null)
        {
            authenticationProperties.Items[tokenNames.DPoPKey] = token.DPoPJsonWebKey.ToString();
        }
        authenticationProperties.Items[tokenNames.Expires] = token.Expiration.ToString("o", CultureInfo.InvariantCulture);

        if (token.RefreshToken != null)
        {
            authenticationProperties.Items[tokenNames.RefreshToken] = token.RefreshToken.ToString();
        }

        var authenticationScheme = await GetSchemeAsync(parameters, ct);
        var cookieOptions = cookieOptionsMonitor.Get(authenticationScheme.ToString());

        if (authenticationProperties.AllowRefresh == true ||
            (authenticationProperties.AllowRefresh == null && cookieOptions.SlidingExpiration))
        {
            // this will allow the cookie to be issued with a new issued (and thus a new expiration)
            authenticationProperties.IssuedUtc = null;
            authenticationProperties.ExpiresUtc = null;
        }

        authenticationProperties.Items.Remove(TokenNamesKey);
        var allTokenNames = authenticationProperties.Items
            .Where(item => item.Key.StartsWith(TokenPrefix))
            .Select(item => item.Key.Substring(TokenPrefix.Length));
        authenticationProperties.Items.Add(new KeyValuePair<string, string?>(TokenNamesKey, string.Join(";", allTokenNames)));
    }

    // If we are using the challenge scheme, we try to get the token 2 ways
    // (with and without the suffix). This is necessary because ASP.NET
    // itself does not set the suffix, so we might not have one at all.
    private static string? GetTokenValue(List<KeyValuePair<string, string?>> tokens, string key, bool appendChallengeScheme, UserTokenRequestParameters? parameters)
    {
        string? token = null;

        if (appendChallengeScheme)
        {
            var scheme = parameters?.ChallengeScheme ?? throw new InvalidOperationException("Attempt to append challenge scheme to token names, but no challenge scheme specified in UserTokenRequestParameters");
            token = tokens.SingleOrDefault(t => t.Key == ChallengeSuffix(key, scheme)).Value;
        }

        if (token.IsMissing())
        {
            token = tokens.SingleOrDefault(t => t.Key == key).Value;
        }

        return token;
    }

    /// <summary>
    /// Confirm application has opted in to UseChallengeSchemeScopedTokens and a
    /// ChallengeScheme is provided upon storage and retrieval of tokens.
    /// </summary>
    /// <param name="parameters"></param>
    /// <returns></returns>
    protected virtual bool AppendChallengeSchemeToTokenNames(UserTokenRequestParameters? parameters) =>
        tokenManagementOptionsMonitor.CurrentValue.UseChallengeSchemeScopedTokens
        && parameters?.ChallengeScheme != null;

    /// <inheritdoc/>
    public async Task<Scheme> GetSchemeAsync(
        UserTokenRequestParameters? parameters = null,
        CT ct = default)
    {
        if (parameters?.SignInScheme != null)
        {
            return parameters.SignInScheme.Value;
        }

        var scheme = (await schemeProvider.GetDefaultSignInSchemeAsync().ConfigureAwait(false))?.Name ??
               throw new InvalidOperationException("No sign in scheme configured");

        return Scheme.Parse(scheme);
    }

    /// <inheritdoc/>
    public void RemoveUserToken(AuthenticationProperties authenticationProperties, UserTokenRequestParameters? parameters = null)
    {
        var names = GetTokenNamesWithScheme(parameters);
        authenticationProperties.Items.Remove(names.Token);
        authenticationProperties.Items.Remove(names.TokenType);
        authenticationProperties.Items.Remove(names.Expires);

        // The DPoP key and refresh token are shared with all resources, so we
        // can only delete them if no other tokens with a different resource
        // exist. The key and refresh token are shared for all resources within
        // a challenge scheme if we are using a challenge scheme.

        var keys = authenticationProperties.Items.Keys.Where(k =>
            k.StartsWith(NamePrefix(OpenIdConnectParameterNames.AccessToken)));

        var usingChallengeSuffix = AppendChallengeSchemeToTokenNames(parameters);
        if (usingChallengeSuffix)
        {
            var challengeScheme = parameters?.ChallengeScheme ?? throw new InvalidOperationException("Attempt to use challenge scheme in token names, but no challenge scheme specified in UserTokenRequestParameters");
            var challengeSuffix = $"||{challengeScheme}";
            keys = keys.Where(k => k.EndsWith(challengeSuffix));
        }

        // If we see a resource separator now, we know there are other resources
        // using the refresh token and/or dpop key and so we shouldn't delete
        // them
        var otherResourcesExist = keys.Any(k => k.Contains("::"));

        if (!otherResourcesExist)
        {
            authenticationProperties.Items.Remove(names.DPoPKey);
            authenticationProperties.Items.Remove(names.RefreshToken);
        }
    }

    private TokenNames GetTokenNamesWithoutScheme(UserTokenRequestParameters? parameters = null) => new(
        Token: NamePrefixAndResourceSuffix(OpenIdConnectParameterNames.AccessToken, parameters),
        TokenType: NamePrefixAndResourceSuffix(OpenIdConnectParameterNames.TokenType, parameters),
        Expires: NamePrefixAndResourceSuffix("expires_at", parameters),
        ClientId: NamePrefixAndResourceSuffix(OpenIdConnectParameterNames.ClientId, parameters),
        IdentityToken: NamePrefixAndResourceSuffix(OpenIdConnectParameterNames.IdToken, parameters),

        // Note that we are not including the resource suffix because there
        // is no per-resource refresh token or dpop key
        RefreshToken: NamePrefix(OpenIdConnectParameterNames.RefreshToken),
        DPoPKey: NamePrefix(DPoPKeyName)
    );

    private TokenNames GetTokenNamesWithScheme(TokenNames names, UserTokenRequestParameters? parameters = null)
    {
        if (AppendChallengeSchemeToTokenNames(parameters))
        {
            // parameters?.ChallengeScheme should not be null after the call to AppendChallengeSchemeToTokenNames
            // We check for that in the default implementation of AppendChallengeSchemeToTokenNames, but if an override
            // didn't, that's an exception
            var challengeScheme = parameters?.ChallengeScheme ?? throw new InvalidOperationException("Attempt to append challenge scheme to token names, but no challenge scheme specified in UserTokenRequestParameters");

            names = new TokenNames(
                Token: ChallengeSuffix(names.Token, challengeScheme),
                TokenType: ChallengeSuffix(names.TokenType, challengeScheme),
                DPoPKey: ChallengeSuffix(names.DPoPKey, challengeScheme),
                Expires: ChallengeSuffix(names.Expires, challengeScheme),
                RefreshToken: ChallengeSuffix(names.RefreshToken, challengeScheme),
                ClientId: ChallengeSuffix(names.ClientId, challengeScheme),
                IdentityToken: ChallengeSuffix(names.IdentityToken, challengeScheme));
        }
        return names;
    }

    private TokenNames GetTokenNamesWithScheme(UserTokenRequestParameters? parameters = null)
    {
        var names = GetTokenNamesWithoutScheme(parameters);
        return GetTokenNamesWithScheme(names, parameters);
    }
}
