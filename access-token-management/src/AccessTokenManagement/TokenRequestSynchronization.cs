// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Concurrent;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Default implementation for token request synchronization primitive
/// </summary>
internal class TokenRequestSynchronization : ITokenRequestSynchronization
{
    // this is what provides the synchronization; assumes this service is a singleton in DI.
    ConcurrentDictionary<string, Lazy<Task<ClientCredentialsToken>>> _dictionary { get; } = new();

    /// <inheritdoc/>
    public async Task<ClientCredentialsToken> SynchronizeAsync(string name, Func<Task<ClientCredentialsToken>> func)
    {
        try
        {
            return await _dictionary.GetOrAdd(name, _ => new Lazy<Task<ClientCredentialsToken>>(func)).Value.ConfigureAwait(false);
        }
        finally
        {
            _dictionary.TryRemove(name, out _);
        }
    }
}
