// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

[JsonConverter(typeof(StringValueJsonConverter<AccessTokenString>))]
public readonly record struct AccessTokenString : IStronglyTypedString<AccessTokenString>
{
    public override string ToString() => Value;

    // Officially, there's no max length for JWTs, but 32k is a good limit
    public const int MaxLength = 32 * 1024; // 32k

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(MaxLength)
    ];

    public static implicit operator AccessTokenString(string value) => Parse(value);

    public AccessTokenString() => throw new InvalidOperationException("Can't create null value");
    private AccessTokenString(string value) => Value = value;

    private string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out AccessTokenString? parsed, out string[] errors) =>
        IStronglyTypedString<AccessTokenString>.TryBuildValidatedObject(value, Validators, out parsed, out errors);


    static AccessTokenString IStronglyTypedString<AccessTokenString>.Create(string result) => new(result);

    public static AccessTokenString Parse(string value) => StringParsers<AccessTokenString>.Parse(value);
}
