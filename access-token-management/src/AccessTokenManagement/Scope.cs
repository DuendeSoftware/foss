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
public readonly partial record struct Scope : IStronglyTypedValue<Scope>
{
    public const int MaxLength = 1024;

    /// <summary>
    /// Convenience method for converting a <see cref="Scope"/> into a string.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator string(Scope value) => value.ToString();

    public override string ToString() => Value;

    // According to RFC 6749, the scope is a space-separated list of scope-token(s).
    // Reference: https://datatracker.ietf.org/doc/html/rfc6749#section-3.3
    // scope       = scope-token *( SP scope-token )
    // scope-token = 1*( %x21 / %x23-5B / %x5D-7E )
    [GeneratedRegex(@"^[\x21\x23-\x5B\x5D-\x7E]+(?: [\x21\x23-\x5B\x5D-\x7E]+)*$")]
    private static partial Regex _validScope();

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(MaxLength),
        ValidationRules.Regex(_validScope(), "The string is not a valid scope. Scopes are space separated and can only contain - (dash). (dot)_ (underscore)~ (tilde)+ (plus)/ (slash): (colon)")
    ];

    /// <summary>
    /// You can't directly create this type.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public Scope() => throw new InvalidOperationException("Can't create null value");

    private Scope(string value) => Value = value;

    private string Value { get; }

    /// <summary>
    /// Parses a value to a <see cref="Scope"/>. This method will return false if the value is invalid
    /// and also includes a list of errors. This is useful for validating user input or other scenarios where you want to provide feedback
    /// </summary>
    public static bool TryParse(string value, [NotNullWhen(true)] out Scope? parsed, out string[] errors) =>
        IStronglyTypedValue<Scope>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    static Scope IStronglyTypedValue<Scope>.Create(string result) => new(result);

    /// <summary>
    /// Parses a value to a <see cref="Scope"/>. This will throw an exception if the string is not valid.
    /// </summary>
    public static Scope Parse(string value) => StringParsers<Scope>.Parse(value);

    /// <summary>
    /// Parses a value to a <see cref="Scope"/>. This will return null if the provided string
    /// is null or whitespace. This is a convenience method for when you want to parse a value that may
    /// contain null or whitespace strings.
    /// </summary>
    public static Scope? ParseOrDefault(string? value) => StringParsers<Scope>.ParseOrDefault(value);
}
