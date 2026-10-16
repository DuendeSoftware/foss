// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Base type for token acquisition results.
/// </summary>
public abstract record TokenResult
{
    /// <summary>
    /// Creates a failed token result.
    /// </summary>
    /// <param name="error">The OAuth/OIDC error code.</param>
    /// <param name="errorDescription">An optional error description.</param>
    /// <returns>A failure result.</returns>
    public static FailedResult Failure(string error, string? errorDescription = null)
        => new(error, errorDescription);

    /// <summary>
    /// Creates a successful token result.
    /// </summary>
    /// <typeparam name="T">The token type.</typeparam>
    /// <param name="token">The token value.</param>
    /// <returns>A success result.</returns>
    public static TokenResult<T> Success<T>(T token) where T : class
        => token;
}

/// <summary>
/// Represents the result of a token request. It can either be a token or a failure.
/// Note, only protocol failures are expressed as failures. Not all possible exceptions
/// are caught and translated to a failure. For example, if the token endpoint is not reachable,
/// or if you've misconfigured the library, you may still get an exception. 
/// </summary>
/// <typeparam name="T">The token type.</typeparam>
public sealed record TokenResult<T> : TokenResult
    where T : class
{
    private TokenResult(T input) => Token = input;

    private TokenResult(FailedResult failure) => FailedResult = failure;

    /// <summary>
    /// Indicates whether the token request succeeded.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Token))]
    [MemberNotNullWhen(false, nameof(FailedResult))]
    public bool Succeeded => FailedResult == null;

    /// <summary>
    /// The failure details when <see cref="Succeeded"/> is <see langword="false"/>.
    /// </summary>
    public FailedResult? FailedResult { get; }

    /// <summary>
    /// The token value when <see cref="Succeeded"/> is <see langword="true"/>.
    /// </summary>
    public T? Token { get; }

    /// <summary>
    /// Converts a token into a successful <see cref="TokenResult{T}"/>.
    /// </summary>
    /// <param name="input">The token value.</param>
    public static implicit operator TokenResult<T>(T input) => new(input);

    /// <summary>
    /// Converts a failure into a <see cref="TokenResult{T}"/>.
    /// </summary>
    /// <param name="failure">The failure details.</param>
    public static implicit operator TokenResult<T>(FailedResult failure) => new(failure);

    /// <summary>
    /// Determines whether this result succeeded and returns the token when it did.
    /// </summary>
    /// <param name="result">The token result when successful.</param>
    /// <returns><see langword="true"/> when successful; otherwise <see langword="false"/>.</returns>
    public bool WasSuccessful(out T result)
    {
        if (Succeeded)
        {
            result = Token;
            return true;
        }

        result = null!;
        return false;
    }

    /// <summary>
    /// Determines whether this result succeeded and returns either token or failure details.
    /// </summary>
    /// <param name="result">The token result when successful; otherwise <see langword="null"/>.</param>
    /// <param name="failure">The failure result when unsuccessful; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when successful; otherwise <see langword="false"/>.</returns>
    public bool WasSuccessful([NotNullWhen(true)] out T? result, [NotNullWhen(false)] out FailedResult? failure)
    {
        if (Succeeded)
        {
            failure = null;
            result = Token;
            return true;
        }

        failure = FailedResult;
        result = null;
        return false;
    }

}
