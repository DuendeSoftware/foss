// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement.Types;

public record IdentityTokenString : IStringValue<IdentityTokenString>
{
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        // Officially, there's no max length for JWTs, but 32k is a good limit
        ValidationRules.MaxLength(32 * 1024)
    ];

    private IdentityTokenString(string value) => Value = value;

    public string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out IdentityTokenString? parsed, out string[] errors) =>
        IStringValue<IdentityTokenString>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    public static implicit operator IdentityTokenString(string value) => Parse(value);

    public static IdentityTokenString Load(string result) => new(result);

    public static IdentityTokenString Parse(string value) => StringParsers<IdentityTokenString>.Parse(value);

    public static IdentityTokenString? ParseOrDefault(string? value) => StringParsers<IdentityTokenString>.ParseOrDefault(value);
}
