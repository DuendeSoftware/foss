// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.Tests;

public class TestDPoPNonceStore : IDPoPNonceStore
{
    public Task<string?> GetNonceAsync(DPoPNonceContext context, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

    public Task StoreNonceAsync(DPoPNonceContext context, string nonce, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
