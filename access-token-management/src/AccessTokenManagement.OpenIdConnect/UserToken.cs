// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Duende.AccessTokenManagement.DPoP;

namespace Duende.AccessTokenManagement.OpenIdConnect;

public sealed record UserToken : AccessTokenRequestHandler.IToken
{
    public required AccessTokenString AccessToken { get; init; }

    public ProofKeyString? DPoPJsonWebKey { get; init; }

    public required DateTimeOffset Expiration { get; init; }

    public Scope? Scope { get; init; }

    public required ClientId ClientId { get; init; }

    public required AccessTokenType? AccessTokenType { get; init; }

    public required RefreshTokenString? RefreshToken { get; init; }

    /// <summary>
    /// The identity token that may be populated by the OP when refreshing the access token. This
    /// value is not stored, but available should some OP's require to send this value, for example
    /// during logout.
    /// </summary>
    public required IdentityTokenString? IdentityToken { get; init; }
}
