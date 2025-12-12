// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

public interface IClientCredentialsCacheDurationStore
{
    TimeSpan GetExpiration(ClientCredentialsCacheKey cacheKey);
    TimeSpan SetExpiration(ClientCredentialsCacheKey cacheKey, DateTimeOffset expiration);
}
