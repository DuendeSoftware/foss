// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Concurrent;


namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

/// <summary>
/// Default implementation for token request concurrency control.
/// If multiple requests ask for the same token to be refreshed, only one will execute.
/// The rest will just wait for this one request. 
/// </summary>
internal class UserTokenRequestConcurrencyControl : IUserTokenRequestConcurrencyControl
{
    // this is what provides the synchronization; assumes this service is a singleton in DI.
    ConcurrentDictionary<string, Lazy<Task<TokenResult<UserToken>>>> Dictionary { get; } = new();

    /// <inheritdoc/>
    public async Task<TokenResult<UserToken>> ExecuteWithConcurrencyControlAsync(UserRefreshToken key, Func<Task<TokenResult<UserToken>>> tokenRetriever, CT ct = default)
    {
        try
        {
            return await Dictionary.GetOrAdd(key.RefreshToken.ToString(), _ => new Lazy<Task<TokenResult<UserToken>>>(tokenRetriever)).Value.ConfigureAwait(false);
        }
        finally
        {
            Dictionary.TryRemove(key.RefreshToken.ToString(), out _);
        }
    }
}
