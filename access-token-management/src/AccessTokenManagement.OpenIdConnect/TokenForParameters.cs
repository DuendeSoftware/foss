// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.AccessTokenManagement.OpenIdConnect;

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
