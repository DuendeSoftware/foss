// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

public readonly record struct RefreshTokenString : IStronglyTypedString<RefreshTokenString>
{
    public const int MaxLength = 4 * 1024;
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        // Officially, there's no max length refresh tokens, but 4k is a good limit
        ValidationRules.MaxLength(MaxLength)
    ];
    public RefreshTokenString() => throw new InvalidOperationException("Can't create null value");

    private RefreshTokenString(string value) => Value = value;
    private string Value { get; }

    public static implicit operator RefreshTokenString(string value) => Parse(value);

    public static bool TryParse(string value, [NotNullWhen(true)] out RefreshTokenString? parsed, out string[] errors) =>
        IStronglyTypedString<RefreshTokenString>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    static RefreshTokenString IStronglyTypedString<RefreshTokenString>.Create(string result) => new(result);
    public static RefreshTokenString Parse(string value) => StringParsers<RefreshTokenString>.Parse(value);
}
