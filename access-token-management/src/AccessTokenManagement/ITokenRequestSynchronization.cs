// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

/// <summary>
/// Service to provide synchronization to token endpoint requests. When concurrent requests are made for the same token, this service
/// de-duplicates the requests and ensures that only one request is made to the token endpoint.
/// </summary>
public interface ITokenRequestSynchronization
{
    /// <summary>
    /// Method to perform synchronization of work.
    /// </summary>
    public Task<ClientCredentialsToken> SynchronizeAsync(string name, Func<Task<ClientCredentialsToken>> func);
}
