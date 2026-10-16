// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Duende.AccessTokenManagement.DPoP;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// A token that's tied to a speficic user. It likely contains claims like
/// sub.
/// </summary>
public sealed record UserToken : AccessTokenRequestHandler.IToken
{
    /// <summary>
    /// The access token that was requested. 
    /// </summary>
    public required AccessToken AccessToken { get; init; }

    /// <summary>
    /// The DPoP JSON Web Key (JWK) associated with this token when the token is DPoP-bound.
    /// </summary>
    public DPoPProofKey? DPoPJsonWebKey { get; init; }

    /// <summary>
    /// Indicates when the token expires. If no expiration is used,
    /// this value is set to DateTimeOffset.MaxValue.
    /// </summary>
    public required DateTimeOffset Expiration { get; init; }

    /// <summary>
    /// The scope that was assigned to the token when requesting it. 
    /// </summary>
    public Scope? Scope { get; init; }

    /// <summary>
    /// The OIDC Client that was used to acquire the token. This is not the same as the client that was used to acquire the
    /// </summary>
    public required ClientId ClientId { get; init; }

    /// <summary>
    /// The type of access token. Typically maps to Bearer or DPoP.
    /// </summary>
    public required AccessTokenType? AccessTokenType { get; init; }

    /// <summary>
    /// The refresh token that the user may have if offline access was requested. 
    /// </summary>
    public RefreshToken? RefreshToken { get; init; }

    /// <summary>
    /// The identity token that may be populated by the OP when refreshing the access token. This
    /// value is not stored, but available should some OP's require to send this value, for example
    /// during logout.
    /// </summary>
    public IdentityToken? IdentityToken { get; init; }
}
