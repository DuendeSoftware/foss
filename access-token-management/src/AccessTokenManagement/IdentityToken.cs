// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

[JsonConverter(typeof(StringValueJsonConverter<IdentityToken>))]
public readonly record struct IdentityToken : IStronglyTypedValue<IdentityToken>
{
    public const int MaxLength = 32 * 1024;
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        // Officially, there's no max length for JWTs, but 32k is a good limit
        ValidationRules.MaxLength(MaxLength)
    ];

    /// <summary>
    /// You can't directly create this type.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public IdentityToken() => throw new InvalidOperationException("Can't create null value");

    private IdentityToken(string value) => Value = value;

    private string Value { get; }

    /// <summary>
    /// Parses a value to a <see cref="IdentityToken"/>. This method will return false if the value is invalid
    /// and also includes a list of errors. This is useful for validating user input or other scenarios where you want to provide feedback
    /// </summary>
    public static bool TryParse(string value, [NotNullWhen(true)] out IdentityToken? parsed, out string[] errors) =>
        IStronglyTypedValue<IdentityToken>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    /// <summary>
    /// Convenience method for converting a <see cref="IdentityToken"/> into a string.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator string(IdentityToken value) => value.ToString();

    static IdentityToken IStronglyTypedValue<IdentityToken>.Create(string result) => new(result);

    /// <summary>
    /// Parses a value to a <see cref="IdentityToken"/>. This will throw an exception if the string is not valid.
    /// </summary>
    public static IdentityToken Parse(string value) => StringParsers<IdentityToken>.Parse(value);

    /// <summary>
    /// Parses a value to a <see cref="IdentityToken"/>. This will return null if the provided string
    /// is null or whitespace. This is a convenience method for when you want to parse a value that may
    /// contain null or whitespace strings.
    /// </summary>
    public static IdentityToken? ParseOrDefault(string? value) => StringParsers<IdentityToken>.ParseOrDefault(value);
}
