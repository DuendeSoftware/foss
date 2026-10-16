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
    /// This method doesn't receive the parameters of the token request, so it can't tell requests for different
    /// resources or scopes apart. Implement
    /// <see cref="ExecuteWithConcurrencyControlAsync(UserRefreshToken, UserTokenRequestParameters, Func{Task{TokenResult{UserToken}}}, CT)"/>
    /// to take them into account.
    /// </remarks>
    public Task<TokenResult<UserToken>> ExecuteWithConcurrencyControlAsync(UserRefreshToken key, Func<Task<TokenResult<UserToken>>> tokenRetriever, CT ct = default);

    /// <summary>
    /// Method to perform synchronization of work, taking into account the parameters of the token request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Concurrent requests that use the same refresh token but different parameters (for example a different
    /// <see cref="TokenRequestParameters.Resource"/> or <see cref="TokenRequestParameters.Scope"/>) result in
    /// different access tokens, so an implementation must not hand the result of one to the other.
    /// </para>
    /// <para>
    /// Requests with different parameters are sent to the token endpoint independently, at the same time, with the
    /// same refresh token. That works when refresh tokens can be reused. Combining different parameters (such as
    /// resource isolation) with one-time-use (rotating) refresh tokens is not supported: the second request would
    /// present a refresh token that has already been redeemed.
    /// </para>
    /// <para>
    /// The default implementation of this interface method calls
    /// <see cref="ExecuteWithConcurrencyControlAsync(UserRefreshToken, Func{Task{TokenResult{UserToken}}}, CT)"/>
    /// and ignores the parameters, so existing implementations keep their behavior.
    /// </para>
    /// </remarks>
    public Task<TokenResult<UserToken>> ExecuteWithConcurrencyControlAsync(
        UserRefreshToken key,
        UserTokenRequestParameters parameters,
        Func<Task<TokenResult<UserToken>>> tokenRetriever,
        CT ct = default) =>
        ExecuteWithConcurrencyControlAsync(key, tokenRetriever, ct);
}
