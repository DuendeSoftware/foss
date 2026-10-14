// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.



namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Service to provide synchronization to token endpoint requests
/// </summary>
public interface IUserTokenRequestConcurrencyControl
{
    /// <summary>
    /// Method to perform synchronization of work.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method doesn't receive the parameters of the token request, so it can't tell requests for
    /// different resources or scopes apart. The retriever also chooses the refresh token and stores the result
    /// itself, so an implementation can't make sure a rotated refresh token is redeemed at most once.
    /// </para>
    /// <para>
    /// Implementations still need this method: the default implementation of
    /// <see cref="ExecuteWithConcurrencyControlAsync(UserRefreshToken, UserTokenRequestParameters, Func{UserRefreshToken, CT, Task{TokenResult{UserToken}}}, Func{UserToken, CT, Task}, CT)"/>
    /// calls it. Implement that overload too to handle different parameters and rotating refresh tokens.
    /// </para>
    /// </remarks>
    [Obsolete(ObsoleteMessage)]
    public Task<TokenResult<UserToken>> ExecuteWithConcurrencyControlAsync(UserRefreshToken key, Func<Task<TokenResult<UserToken>>> tokenRetriever, CT ct = default);

    internal const string ObsoleteMessage =
        "Use the overload that takes UserTokenRequestParameters, a redeem and a store delegate. " +
        "This overload can't tell requests for different resources or scopes apart.";

    /// <summary>
    /// Method to perform synchronization of a refresh token request, taking into account the parameters of the request.
    /// </summary>
    /// <param name="key">The refresh token the caller read from its token store.</param>
    /// <param name="parameters">The parameters of the token request.</param>
    /// <param name="redeem">
    /// Redeems a refresh token at the token endpoint. It receives the refresh token to redeem, which can be newer
    /// than <paramref name="key"/>, and the cancellation token to use. It must not store the result.
    /// </param>
    /// <param name="store">
    /// Stores a successfully redeemed token. An implementation should call it after it has recorded the refresh
    /// token in the result as the newest refresh token, so a request that reads the stored refresh token finds it.
    /// </param>
    /// <param name="ct">Cancels the caller's wait for the result.</param>
    /// <remarks>
    /// <para>
    /// An implementation should make sure a refresh token value is redeemed at most once. When the token service
    /// rotates refresh tokens (one-time use), presenting a redeemed refresh token again fails, and the token service
    /// may treat it as token theft and revoke the whole grant (RFC 9700, section 4.14.2).
    /// </para>
    /// <para>
    /// Requests with the same refresh token and the same parameters can share one result. Requests with the same
    /// refresh token but different parameters (for example a different <see cref="TokenRequestParameters.Resource"/>)
    /// result in different access tokens, so they must not share a result. They should run one after another, each
    /// redeeming the newest refresh token.
    /// </para>
    /// <para>
    /// The default implementation coordinates requests within one process. With more than one server, use an
    /// implementation backed by a distributed lock, or sticky sessions.
    /// </para>
    /// <para>
    /// The default implementation of this interface method calls
    /// <see cref="ExecuteWithConcurrencyControlAsync(UserRefreshToken, Func{Task{TokenResult{UserToken}}}, CT)"/>
    /// with <paramref name="redeem"/> followed by <paramref name="store"/>, and ignores the parameters,
    /// so existing implementations keep their behavior.
    /// </para>
    /// </remarks>
    public Task<TokenResult<UserToken>> ExecuteWithConcurrencyControlAsync(
        UserRefreshToken key,
        UserTokenRequestParameters parameters,
        Func<UserRefreshToken, CT, Task<TokenResult<UserToken>>> redeem,
        Func<UserToken, CT, Task> store,
        CT ct = default) =>
#pragma warning disable CS0618 // Forwarding to the original method is the point of this default implementation.
        ExecuteWithConcurrencyControlAsync(
#pragma warning restore CS0618
            key,
            async () =>
            {
                var result = await redeem(key, ct).ConfigureAwait(false);
                if (result.WasSuccessful(out var token))
                {
                    await store(token, ct).ConfigureAwait(false);
                }

                return result;
            },
            ct);
}
