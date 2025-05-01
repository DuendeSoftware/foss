// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement.Types;

[JsonConverter(typeof(StringValueJsonConverter<AccessTokenString>))]
public record AccessTokenString : IStringValue<AccessTokenString>
{
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        // Officially, there's no max length for JWTs, but 32k is a good limit
        ValidationRules.MaxLength(32 * 1024)
    ];

    public static implicit operator AccessTokenString(string value) => Parse(value);

    private AccessTokenString(string value) => Value = value;

    public string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out AccessTokenString? parsed, out string[] errors) =>
        IStringValue<AccessTokenString>.TryBuildValidatedObject(value, Validators, out parsed, out errors);


    public static AccessTokenString Load(string result) => new(result);

    public static AccessTokenString Parse(string value) => StringParsers<AccessTokenString>.Parse(value);
}
