// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

[JsonConverter(typeof(StringValueJsonConverter<AccessTokenType>))]
public readonly record struct AccessTokenType : IStronglyTypedString<AccessTokenType>
{
    public const int MaxLength = 50;
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(MaxLength),
        ValidationRules.AlphaNumeric()
    ];

    private AccessTokenType(string value) => Value = value;
    public AccessTokenType() => throw new InvalidOperationException("Can't create null value");
    private string Value { get; }

    public static implicit operator AccessTokenType(string value) => Parse(value);

    public static bool TryParse(string value, [NotNullWhen(true)] out AccessTokenType? parsed, out string[] errors) =>
        IStronglyTypedString<AccessTokenType>
            .TryBuildValidatedObject(value, Validators, out parsed, out errors);


    static AccessTokenType IStronglyTypedString<AccessTokenType>.Create(string result) => new(result);

    public static AccessTokenType Parse(string value) =>
        StringParsers<AccessTokenType>.Parse(value);
    public static AccessTokenType? ParseOrDefault(string? value) => StringParsers<AccessTokenType>.ParseOrDefault(value);

    public Scheme ToScheme() => Scheme.Parse(Value);

}
