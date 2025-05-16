// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

[TypeConverter(typeof(StringValueConverter<Scope>))]
[JsonConverter(typeof(StringValueJsonConverter<Scope>))]
public readonly partial record struct Scope : IStronglyTypedString<Scope>
{
    public const int MaxLength = 1024;
    public static implicit operator Scope(string value) => Parse(value);
    public override string ToString() => Value;

    // According to RFC 6749, the scope is a space-separated list of strings.
    // it can only contain characters in the set [A-Za-z0-9\-._~+/:\^|`!#$%&'*]
    [GeneratedRegex(@"^([A-Za-z0-9\-._~+/:\^|`!#$%&'*]+ ?)+$")]
    private static partial Regex _validScope();

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(MaxLength),
        ValidationRules.Regex(_validScope(), "The string is not a valid scope. Scopes are space separated and can only contain - (dash). (dot)_ (underscore)~ (tilde)+ (plus)/ (slash): (colon)")
    ];

    public Scope() => throw new InvalidOperationException("Can't create null value");

    private Scope(string value) => Value = value;

    private string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out Scope? parsed, out string[] errors) =>
        IStronglyTypedString<Scope>.TryBuildValidatedObject(value, Validators, out parsed, out errors);


    static Scope IStronglyTypedString<Scope>.Create(string result) => new(result);

    public static Scope Parse(string value) => StringParsers<Scope>.Parse(value);

    public static Scope? ParseOrDefault(string? value) => StringParsers<Scope>.ParseOrDefault(value);
}
