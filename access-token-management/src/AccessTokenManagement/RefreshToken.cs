// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

[JsonConverter(typeof(StringValueJsonConverter<RefreshToken>))]
public readonly record struct RefreshToken : IStronglyTypedValue<RefreshToken>
{
    public const int MaxLength = 4 * 1024;
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        // Officially, there's no max length refresh tokens, but 4k is a good limit
        ValidationRules.MaxLength(MaxLength)
    ];

    /// <summary>
    /// You can't directly create this type.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public RefreshToken() => throw new InvalidOperationException("Can't create null value");

    private RefreshToken(string value) => Value = value;
    private string Value { get; }

    /// <summary>
    /// Convenience method for converting a <see cref="RefreshToken"/> into a string.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator string(RefreshToken value) => value.ToString();

    /// <summary>
    /// Parses a value to a <see cref="RefreshToken"/>. This method will return false if the value is invalid
    /// and also includes a list of errors. This is useful for validating user input or other scenarios where you want to provide feedback
    /// </summary>
    public static bool TryParse(string value, [NotNullWhen(true)] out RefreshToken? parsed, out string[] errors) =>
        IStronglyTypedValue<RefreshToken>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    static RefreshToken IStronglyTypedValue<RefreshToken>.Create(string result) => new(result);

    /// <summary>
    /// Parses a value to a <see cref="RefreshToken"/>. This will throw an exception if the string is not valid.
    /// </summary>
    public static RefreshToken Parse(string value) => StringParsers<RefreshToken>.Parse(value);
}
