// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Types;

namespace Duende.AccessTokenManagement.OpenIdConnect;
public sealed record UserToken : AccessTokenRequestHandler.IToken
{
    public required AccessTokenString AccessToken { get; init; }
    public DPoPJsonWebKey? DPoPJsonWebKey { get; init; }
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

public sealed record TokenForParameters
{
    public TokenForParameters(UserToken tokenForSpecifiedParameters, UserRefreshToken? refreshToken)
    {
        TokenForSpecifiedParameters = tokenForSpecifiedParameters;
        RefreshToken = refreshToken;
        NoRefreshToken = refreshToken == null;
    }

    public TokenForParameters(UserRefreshToken refreshToken)
    {
        RefreshToken = refreshToken;
        NoRefreshToken = false;
    }


    public UserToken? TokenForSpecifiedParameters { get; }
    public UserRefreshToken? RefreshToken { get; }

    [MemberNotNullWhen(true, nameof(TokenForSpecifiedParameters))]
    [MemberNotNullWhen(false, nameof(RefreshToken))]
    public bool NoRefreshToken { get; private set; }
}

public record UserRefreshToken
{
    public required RefreshTokenString RefreshToken { get; init; }
    public required DPoPJsonWebKey? DPoPJsonWebKey { get; init; }
}
