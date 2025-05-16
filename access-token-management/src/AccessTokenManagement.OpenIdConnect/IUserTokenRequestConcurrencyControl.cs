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
    public Task<TokenResult<UserToken>> ExecuteWithConcurrencyControlAsync(UserRefreshToken key, Func<Task<TokenResult<UserToken>>> tokenRetriever, CT ct = default);
}
