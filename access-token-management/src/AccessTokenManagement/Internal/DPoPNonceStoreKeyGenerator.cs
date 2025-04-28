// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.Internal;

/// <summary>
/// The logic to generate a key to store a DPoP nonce in the Cache. Defaults to
/// <see cref="ClientCredentialsTokenManagementOptions.NonceStoreKeyPrefix"/> + URL + Method.
/// </summary>
/// <param name="options"></param>
internal class DPoPNonceStoreKeyGenerator(IOptions<ClientCredentialsTokenManagementOptions> options) : IDPoPNonceStoreKeyGenerator
{
    public string GenerateKey(DPoPNonceContext context) =>
        $"{options.Value.NonceStoreKeyPrefix}:{context.Url}:{context.Method}";
}
