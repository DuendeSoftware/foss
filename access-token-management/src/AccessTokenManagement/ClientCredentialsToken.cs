// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;

namespace Duende.AccessTokenManagement;

public sealed record ClientCredentialsToken : AccessTokenRequestHandler.IToken
{
    public required AccessTokenString AccessToken { get; init; }
    public required AccessTokenType? AccessTokenType { get; init; }
    public required ProofKeyString? DPoPJsonWebKey { get; init; }
    public required DateTimeOffset Expiration { get; init; }
    public required Scope? Scope { get; init; }
    public required ClientId ClientId { get; init; }
}
