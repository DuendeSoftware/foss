// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;

namespace Duende.AccessTokenManagement.Framework;

public class TestDPoPNonceStore : IDPoPNonceStore
{
    public Task<DPoPNonce?> GetNonceAsync(DPoPNonceContext context, CancellationToken cancellationToken = default)
        => Task.FromResult<DPoPNonce?>(null);

    public Task StoreNonceAsync(DPoPNonceContext context, DPoPNonce nonce, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
