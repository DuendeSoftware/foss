// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.AccessTokenManagement.Types;

public abstract record TokenResult
{
    public abstract bool Succeeded { get; protected set; }

    public static FailedResult Failure(string error, string? errorDescription = null) => new FailedResult(error, errorDescription);
    public static TokenResult<T> Success<T>(T token) where T : class
        => token;
}

public record TokenResult<T> : TokenResult
    where T : class
{

    [MemberNotNullWhen(true, nameof(Token))]
    [MemberNotNullWhen(false, nameof(FailedResult))]
    public override bool Succeeded { get; protected set; }

    [MemberNotNullWhen(false, nameof(Token))]
    [MemberNotNullWhen(true, nameof(FailedResult))]
    public bool IsError => !Succeeded;
    public FailedResult? FailedResult { get; private set; }
    public T? Token { get; private set; }

    public static implicit operator T(TokenResult<T> input)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (!input.Succeeded)
        {
            throw new InvalidOperationException("Failed to get token: " + input.FailedResult);
        }

        return input.Token;
    }

    public static implicit operator TokenResult<T>(T input) => new()
    {
        Token = input,
        Succeeded = true
    };

    public static implicit operator TokenResult<T>(FailedResult failure) => new()
    {
        FailedResult = failure
    };

    public bool WasSuccessful(out T result)
    {
        if (Succeeded)
        {
            result = Token;
            return true;
        }

        result = default(T)!;
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
        result = default(T);
        return false;
    }

}
