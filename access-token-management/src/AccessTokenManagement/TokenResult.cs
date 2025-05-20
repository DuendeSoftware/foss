// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.AccessTokenManagement;

public abstract record TokenResult
{
    public static FailedResult Failure(string error, string? errorDescription = null)
        => new(error, errorDescription);

    public static TokenResult<T> Success<T>(T token) where T : class
        => token;
}

/// <summary>
/// Represents the result of a token request. It can either be a token or a failure.
/// Note, only protocol failures are expressed as failures. Not all possible exceptions
/// are caught and translated to a failure. For example, if the token endpoint is not reachable,
/// or if you've misconfigured the library, you may still get an exception. 
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed record TokenResult<T> : TokenResult
    where T : class
{
    private TokenResult(T input) => Token = input;

    private TokenResult(FailedResult failure) => FailedResult = failure;


    [MemberNotNullWhen(true, nameof(Token))]
    [MemberNotNullWhen(false, nameof(FailedResult))]
    public bool Succeeded => FailedResult == null;

    public FailedResult? FailedResult { get; }

    public T? Token { get; }

    public static implicit operator TokenResult<T>(T input) => new(input);

    public static implicit operator TokenResult<T>(FailedResult failure) => new(failure);

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
