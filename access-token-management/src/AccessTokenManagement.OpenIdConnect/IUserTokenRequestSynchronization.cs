// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.Types;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Service to provide synchronization to token endpoint requests
/// </summary>
public interface IUserTokenRequestSynchronization
{
    /// <summary>
    /// Method to perform synchronization of work.
    /// </summary>
    public Task<TokenResult<UserToken>> SynchronizeAsync(UserRefreshToken refreshToken, Func<Task<TokenResult<UserToken>>> func, CancellationToken cancellationToken = default);
}
