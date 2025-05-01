// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Duende.AccessTokenManagement.Types;

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

/// <summary>
/// Default implementation for token request synchronization primitive
/// </summary>
internal class UserTokenRequestSynchronization : IUserTokenRequestSynchronization
{
    // this is what provides the synchronization; assumes this service is a singleton in DI.
    ConcurrentDictionary<string, Lazy<Task<TokenResult<UserToken>>>> Dictionary { get; } = new();

    /// <inheritdoc/>
    public async Task<TokenResult<UserToken>> SynchronizeAsync(UserRefreshToken token, Func<Task<TokenResult<UserToken>>> func, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Dictionary.GetOrAdd(token.RefreshToken.Value, _ => new Lazy<Task<TokenResult<UserToken>>>(func)).Value.ConfigureAwait(false);
        }
        finally
        {
            Dictionary.TryRemove(token.RefreshToken.Value, out _);
        }
    }
}
