// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using Duende.AccessTokenManagement.OTel;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement.Internal;

internal static partial class LogMessages
{
    [LoggerMessage(
        Message = $"Cannot authenticate scheme: {{{OTelParameters.Scheme}}} to acquire user access token.")]
    public static partial void CannotAuthenticateSchemeToAcquireUserAccessToken(
        this ILogger logger, LogLevel logLevel, Scheme? scheme);

    [LoggerMessage(
        Message = $"Authentication result properties are null for scheme: {{{OTelParameters.Scheme}}} after authentication.")]
    public static partial void AuthenticationResultPropertiesAreNullAfterAuthenticate(
        this ILogger logger, LogLevel logLevel, Scheme? scheme);

    [LoggerMessage(
        Message = "Failed to get a UserToken because no tokens found in cookie properties. SaveTokens must be enabled for automatic token refresh.")]
    public static partial void FailedToGetUserTokenDueToMissingTokensInCookie(this ILogger logger, LogLevel level);

    [LoggerMessage(
        Message = $"Error revoking refresh token. Error = {{{OTelParameters.Error}}}")]
    public static partial void FailedToRevokeAccessToken(this ILogger logger, LogLevel logLevel, string? error);

    [LoggerMessage(
        Message = $"Sending DPoP proof token in request to endpoint: {{{OTelParameters.Url}}}")]
    public static partial void SendingDPoPProofToken(this ILogger logger, LogLevel logLevel, Uri? url);

    [LoggerMessage(
        Message = $"Failed to create DPoP proof token for request to endpoint: {{{OTelParameters.Url}}}")]
    public static partial void FailedToCreateDPopProofToken(this ILogger logger, LogLevel logLevel, Uri? url);


    [LoggerMessage(
        Message = $"Sending Access token of type {{{OTelParameters.TokenType}}} to endpoint: {{{OTelParameters.Url}}}.")]
    public static partial void SendAccessTokenToEndpoint(this ILogger logger, LogLevel logLevel, Uri? url, string? tokenType);

    [LoggerMessage(
        Message = $"Failed to obtain an access token while sending the request. Error: {{{OTelParameters.Error}}}, ErrorDescription {{{OTelParameters.ErrorDescription}}}")]
    public static partial void FailedToObtainAccessTokenWhileSendingRequest(this ILogger logger, LogLevel logLevel, string? error, string? errorDescription);


    [LoggerMessage(
        Message = "While sending a request, received UnAuthorized after acquiring a new access token. This means the access token is somehow wrong and is not accepted.")]
    public static partial void AccessTokenHandlerAuthenticationFailed(this ILogger logger, LogLevel logLevel);


    [LoggerMessage(
        Message = $"DPoP nonce error: '{{{OTelParameters.Error}}}'. Retrying using new nonce")]
    public static partial void RequestFailedWithDPoPErrorWillRetry(this ILogger logger, LogLevel logLevel, string? error);

    [LoggerMessage(
        Message = "Token not accepted while sending request. Retrying with new access token. ")]
    public static partial void TokenNotAcceptedWhenSendingRequest(this ILogger logger, LogLevel logLevel);

    /// <summary>
    /// Logs the refreshing of a refresh token. Note, the actual refresh token is not logged, but a hash of the token.
    /// Because hashing can be costly, we're only doing this when the log level is Trace. This is not something the source generators
    /// can do, so we're wrapping this in a method.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="logLevel"></param>
    /// <param name="refreshToken"></param>
    /// <param name="hashAlgorithm"></param>
    public static void RefreshingAccessTokenUsingRefreshToken(this ILogger logger, LogLevel logLevel, RefreshTokenString refreshToken, Func<string, string> hashAlgorithm)
    {
        if (logger.IsEnabled(logLevel))
        {
            RefreshingTokenUsingRefreshTokenImplementation(logger, logLevel, hashAlgorithm(refreshToken.ToString()));
        }
    }

    [LoggerMessage(
        Message = $"Refreshing access token using refresh token: hash={{{OTelParameters.TokenHash}}}")]
    private static partial void RefreshingTokenUsingRefreshTokenImplementation(this ILogger logger, LogLevel logLevel, string tokenHash);

    [LoggerMessage(
        Message = $"Sending Refresh token request to: {{{OTelParameters.Url}}}")]
    public static partial void SendingRefreshTokenRequest(this ILogger logger, LogLevel logLevel, Uri? url);

    [LoggerMessage(
        Message = $"DPoP error '{{{OTelParameters.Error}}}' during token refresh. Retrying with server nonce")]
    public static partial void DPoPErrorDuringTokenRefreshWillRetryWithServerNonce(this ILogger logger, LogLevel logLevel, string? error);

    [LoggerMessage(
        Message = $"Failed to get DPoP Nonce because server didn't respond with ok. StatusCode was: {{{OTelParameters.StatusCode}}}")]
    public static partial void FailedToGetDPoPNonce(this ILogger logger, LogLevel logLevel, HttpStatusCode statusCode);

    /// <summary>
    /// Logs the revocation of a refresh token. Note, the actual refresh token is not logged, but a hash of the token.
    /// Because hashing can be costly, we're only doing this when the log level is Trace. This is not something the source generators
    /// can do, so we're wrapping this in a method.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="logLevel"></param>
    /// <param name="refreshToken"></param>
    /// <param name="hashAlgorithm"></param>
    public static void RevokingRefreshToken(this ILogger logger, LogLevel logLevel, RefreshTokenString refreshToken, Func<string, string> hashAlgorithm)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            RevokingRefreshTokenImplementation(logger, logLevel, hashAlgorithm(refreshToken.ToString()));
        }
    }

    [LoggerMessage(
        Message = $"Revoking refresh token: hash={{{OTelParameters.TokenHash}}}")]
    private static partial void RevokingRefreshTokenImplementation(this ILogger logger, LogLevel logLevel, string tokenHash);

    [LoggerMessage(
        Message = $"Sending Token revocation request to: {{{OTelParameters.Url}}}")]
    public static partial void SendingTokenRevocationRequest(this ILogger logger, LogLevel logLevel, Uri url);

    [LoggerMessage(
        Message = "Starting user token acquisition")]
    public static partial void StartingUserTokenAcquisition(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        Message = "Cannot retrieve token: No active user")]
    public static partial void CannotRetrieveAccessTokenDueToNoActiveUser(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        Message = $"Cannot retrieve token: No token data found in user token store for user {{{OTelParameters.User}}}.")]
    public static partial void CannotRetrieveAccessTokenDueToNoTokenDataFound(this ILogger logger, LogLevel logLevel, string user);

    [LoggerMessage(
        Message = $"No refresh token found in user token store for user {{{OTelParameters.User}}} / resource {{{OTelParameters.Resource}}}. Returning current access token.")]
    public static partial void NoRefreshTokenAvailableWillNotRefresh(this ILogger logger, LogLevel logLevel, string user, Resource resource);

    [LoggerMessage(
        Message = $"No access token found in user token store for user {{{OTelParameters.User}}} / resource {{{OTelParameters.Resource}}}. Trying to refresh.")]
    public static partial void NoAccessTokenFoundWillRefresh(this ILogger logger, LogLevel logLevel, string user, Resource resource);

    [LoggerMessage(
        Message = $"Token for user {{{OTelParameters.User}}} will be refreshed. Expiration: {{{OTelParameters.Expiration}}}, ForceRenewal:{{{OTelParameters.ForceRenewal}}}")]
    public static partial void TokenNeedsRefreshing(this ILogger logger, LogLevel logLevel, string user, DateTimeOffset? expiration, ForceTokenRenewal forceRenewal);

    [LoggerMessage(
        Message = $"Returning refreshed token for user: {{{OTelParameters.User}}}")]
    public static partial void ReturningRefreshedToken(this ILogger logger, LogLevel logLevel, string user);

    [LoggerMessage(
        Message = $"Returning current token for user: {{{OTelParameters.User}}}")]
    public static partial void ReturningCurrentTokenForUser(this ILogger logger, LogLevel logLevel, string user);

    [LoggerMessage(
        Message = $"Error refreshing access token. Error = {{{OTelParameters.Error}}}, Description: {{{OTelParameters.ErrorDescription}}}")]
    public static partial void FailedToRefreshAccessToken(this ILogger logger, LogLevel logLevel, string? error, string? errorDescription);

    [LoggerMessage(
        Message = $"Access Token of type {{{OTelParameters.TokenType}}} refreshed with expiration: {{{OTelParameters.Expiration}}}")]
    public static partial void UserAccessTokenRefreshed(this ILogger logger, LogLevel logLevel, AccessTokenType? tokenType, DateTimeOffset expiration);

    [LoggerMessage(
        Message = "The authorization server has supplied a new nonce on a successful response, which will be stored and used in future requests to the authorization server")]
    public static partial void AuthorizationServerSuppliedNewNonce(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        Message = $"Caching access token for client: {{{OTelParameters.ClientName}}}. Expiration: {{{OTelParameters.Expiration}}}")]
    public static partial void CachingAccessToken(this ILogger logger, LogLevel logLevel, ClientName clientName, DateTimeOffset expiration);

    [LoggerMessage(
        Message = $"Will not cache token result with error for {{{OTelParameters.ClientName}}}. Error = {{{OTelParameters.Error}}}, Description: {{{OTelParameters.ErrorDescription}}}")]
    public static partial void WillNotCacheTokenResultWithError(this ILogger logger, LogLevel logLevel, ClientName clientName,
        string error, string? errorDescription);

    [LoggerMessage(
        Message = $"An exception has occurred while reading ClientCredentialsToken value from the cache for client {{{OTelParameters.ClientName}}}. The call will be executed without the cache.")]
    public static partial void ExceptionWhileReadingFromCache(this ILogger logger, LogLevel logLevel, Exception ex, ClientName clientName);


    [LoggerMessage(
        Message = $"Error requesting access token for client {{{OTelParameters.ClientName}}}. Error = {{{OTelParameters.Error}}}, Description: {{{OTelParameters.ErrorDescription}}}")]
    public static partial void FailedToRequestAccessTokenForClient(this ILogger logger, LogLevel logLevel, ClientName clientName, string? error, string? errorDescription);

    [LoggerMessage(
        Message = $"Error trying to set token in cache for client {{{OTelParameters.ClientName}}}")]
    public static partial void ErrorSettingTokenInCache(this ILogger logger, LogLevel logLevel, Exception ex, ClientName clientName);

    [LoggerMessage(
        Message = $"Cache hit for obtaining access token for client: {{{OTelParameters.ClientName}}}")]
    public static partial void CacheHitForObtainingAccessToken(this ILogger logger, LogLevel logLevel, ClientName clientName);

    [LoggerMessage(
        Message = $"Cache hit for DPoP nonce for URL: {{{OTelParameters.Url}}}, method: {{{OTelParameters.Method}}}")]
    public static partial void CacheHitForDPoPNonce(this ILogger logger, LogLevel logLevel, Uri url, HttpMethod method);

    [LoggerMessage(
        Message = $"Writing DPoP nonce to Cache for URL: {{{OTelParameters.Url}}}, method: {{{OTelParameters.Method}}}. Expiration: {{{OTelParameters.Expiration}}}")]
    public static partial void WritingNonceToCache(this ILogger logger, LogLevel logLevel, Uri url, HttpMethod method, DateTimeOffset expiration);

    [LoggerMessage(
        Message = $"Writing DPoP nonce to Cache for URL: {{{OTelParameters.Url}}}, method: {{{OTelParameters.Method}}}. Expiration: {{{OTelParameters.Expiration}}}")]
    public static partial void WritingNonceToCache(this ILogger logger, LogLevel logLevel, Uri url, HttpMethod method, TimeSpan expiration);

    [LoggerMessage(
        Message = $"Cache miss for DPoP nonce for URL: {{{OTelParameters.Url}}}, method: {{{OTelParameters.Method}}}")]
    public static partial void CacheMissForDPoPNonce(this ILogger logger, LogLevel logLevel, Uri url, HttpMethod method);

    [LoggerMessage(
        Message = $"Failed to parse the cached Nonce '{{{OTelParameters.Value}}}' for URL: {{{OTelParameters.Url}}}, method: {{{OTelParameters.Method}}}. Error: {{{OTelParameters.Error}}}")]
    public static partial void CachedNonceParseFailure(this ILogger logger, LogLevel logLevel, Uri url, HttpMethod method, string value, string error);

    [LoggerMessage(
        Message = $"Error parsing cached access token for client {{{OTelParameters.ClientName}}}")]
    public static partial void FailedToCacheAccessToken(this ILogger logger, LogLevel logLevel, Exception ex, ClientName clientName);

    [LoggerMessage(
        Message = $"Cache miss while retrieving access token for client: {{{OTelParameters.ClientName}}}")]
    public static partial void CacheMissWhileRetrievingAccessToken(this ILogger logger, LogLevel logLevel, ClientName clientName);

    [LoggerMessage(
        Message = "Creating DPoP proof token for token request.")]
    public static partial void CreatingDPoPProofToken(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        Message = $"Requesting client credentials access token at endpoint: {{{OTelParameters.Url}}}")]
    public static partial void RequestingClientCredentialsAccessToken(this ILogger logger, LogLevel logLevel, Uri url);

    [LoggerMessage(
        Message = $"Client Credentials token of type '{{{OTelParameters.TokenType}}}' for Client: {{{OTelParameters.ClientName}}} retrieved with expiration {{{OTelParameters.Expiration}}} ")]
    public static partial void ClientCredentialsTokenForClientRetrieved(this ILogger logger, LogLevel logLevel, ClientName clientName, AccessTokenType? tokenType, DateTimeOffset expiration);

    [LoggerMessage(
        Message = $"Failed to obtain token from cache for client {{{OTelParameters.ClientName}}} using cacheKey {{{OTelParameters.CacheKey}}}. Will obtain new token.")]
    public static partial void FailedToObtainTokenFromCache(this ILogger logger, LogLevel logLevel, Exception ex, ClientName clientName, ClientCredentialsCacheKey cacheKey);

    [LoggerMessage(
        Message = "Failed to parse JsonWebKey")]
    public static partial void FailedToParseJsonWebKey(this ILogger logger, LogLevel logLevel, Exception ex);

    [LoggerMessage(
        Message = "Failed to create thumbprint from JSON web key.")]
    public static partial void FailedToCreateThumbprintFromJsonWebKey(this ILogger logger, LogLevel logLevel, Exception ex);
}
