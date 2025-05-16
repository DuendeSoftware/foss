// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

public readonly record struct IdentityTokenString : IStronglyTypedString<IdentityTokenString>
{
    public const int MaxLength = 32 * 1024;
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        // Officially, there's no max length for JWTs, but 32k is a good limit
        ValidationRules.MaxLength(MaxLength)
    ];

    public IdentityTokenString() => throw new InvalidOperationException("Can't create null value");

    private IdentityTokenString(string value) => Value = value;

    private string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out IdentityTokenString? parsed, out string[] errors) =>
        IStronglyTypedString<IdentityTokenString>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    public static implicit operator IdentityTokenString(string value) => Parse(value);

    static IdentityTokenString IStronglyTypedString<IdentityTokenString>.Create(string result) => new(result);

    public static IdentityTokenString Parse(string value) => StringParsers<IdentityTokenString>.Parse(value);

    public static IdentityTokenString? ParseOrDefault(string? value) => StringParsers<IdentityTokenString>.ParseOrDefault(value);
}
