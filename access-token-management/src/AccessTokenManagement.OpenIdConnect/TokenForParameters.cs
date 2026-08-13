// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Represents the result when asking for a token for a specific set of parameters, such
/// as scope, resource and possibly others. 
///
/// You'll get either an UserToken with an optional UserRefreshToken, or a UserRefreshToken.
///
/// It's not possible to get back an optional user token and no refresh token, because that
/// would be a failure. 
/// 
/// If you get back only a UserRefreshToken, it means that the token for the specified parameters was not found
/// in the cache and you'll need the refresh token to acquire it. 
/// 
/// </summary>
public sealed record TokenForParameters
{
    /// <summary>
    /// A token has been found for the specified parameters. If the user has a refresh token,
    /// that's also included. 
    /// </summary>
    public TokenForParameters(UserToken tokenForSpecifiedParameters, UserRefreshToken? refreshToken)
    {
        TokenForSpecifiedParameters = tokenForSpecifiedParameters;
        RefreshToken = refreshToken;
        NoRefreshToken = refreshToken == null;
    }

    /// <summary>
    /// No token has been found for the specified parameters. The refresh token can be used
    /// to get one. 
    /// </summary>
    /// <param name="refreshToken"></param>
    public TokenForParameters(UserRefreshToken refreshToken)
    {
        RefreshToken = refreshToken;
        NoRefreshToken = false;
    }

    /// <summary>
    /// The token for the specified parameters. This is the token that was found in the cache.
    /// </summary>
    public UserToken? TokenForSpecifiedParameters { get; }

    /// <summary>
    /// The user's refresh token. 
    /// </summary>
    public UserRefreshToken? RefreshToken { get; }

    /// <summary>
    /// Indicates whether no refresh token exists.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/>, <see cref="TokenForSpecifiedParameters"/> is populated.
    /// When <see langword="false"/>, <see cref="RefreshToken"/> is populated.
    /// </remarks>
    [MemberNotNullWhen(true, nameof(TokenForSpecifiedParameters))]
    [MemberNotNullWhen(false, nameof(RefreshToken))]
    public bool NoRefreshToken { get; private set; }
}
