// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement.Types;

public record RefreshTokenString : IStringValue<RefreshTokenString>
{
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        // Officially, there's no max length refresh tokens, but 4k is a good limit
        ValidationRules.MaxLength(4 * 1024)
    ];
    private RefreshTokenString(string value) => Value = value;
    public string Value { get; }

    public static implicit operator RefreshTokenString(string value) => Parse(value);

    public static bool TryParse(string value, [NotNullWhen(true)] out RefreshTokenString? parsed, out string[] errors) =>
        IStringValue<RefreshTokenString>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    public static RefreshTokenString Load(string result) => new RefreshTokenString(result);
    public static RefreshTokenString Parse(string value) => StringParsers<RefreshTokenString>.Parse(value);
}
