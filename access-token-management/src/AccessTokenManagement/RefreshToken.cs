// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Represents an OAuth refresh token value.
/// </summary>
[JsonConverter(typeof(StringValueJsonConverter<RefreshToken>))]
public readonly record struct RefreshToken : IStronglyTypedValue<RefreshToken>
{
    /// <summary>
    /// The default maximum supported length for a refresh token string. It is used when no explicit maximum is supplied.
    /// ATM read paths use the configured max length option instead.
    /// </summary>
    public const int MaxLength = 4 * 1024;

    /// <summary>
    /// Returns the wrapped refresh token string.
    /// </summary>
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
        TryParse(value, MaxLength, out parsed, out errors);

    /// <summary>
    /// Parses a value to a <see cref="RefreshToken"/> using the supplied maximum length instead of <see cref="MaxLength"/>.
    /// This method will return false if the value is invalid and also includes a list of errors. It lets code outside
    /// ATM (custom <c>IUserTokenStore</c> implementations, other Duende libraries) honor a configured limit.
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <param name="maxLength">The maximum accepted length. Must be greater than zero.</param>
    /// <param name="parsed">The parsed value, if parsing succeeded.</param>
    /// <param name="errors">The validation errors, if parsing failed.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is zero or negative.</exception>
    public static bool TryParse(string value, int maxLength, [NotNullWhen(true)] out RefreshToken? parsed, out string[] errors)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);
        return IStronglyTypedValue<RefreshToken>.TryBuildValidatedObject(
            value,
            maxLength == MaxLength ? Validators : [ValidationRules.MaxLength(maxLength)],
            out parsed,
            out errors);
    }

    static RefreshToken IStronglyTypedValue<RefreshToken>.Create(string result) => new(result);

    /// <summary>
    /// Parses a value to a <see cref="RefreshToken"/>. This will throw an exception if the string is not valid.
    /// </summary>
    public static RefreshToken Parse(string value) => Parse(value, MaxLength);

    /// <summary>
    /// Parses a value to a <see cref="RefreshToken"/> using the supplied maximum length instead of <see cref="MaxLength"/>.
    /// This will throw an exception if the string is not valid. It lets code outside ATM (custom
    /// <c>IUserTokenStore</c> implementations, other Duende libraries) honor a configured limit.
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <param name="maxLength">The maximum accepted length. Must be greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">The value is not valid.</exception>
    public static RefreshToken Parse(string value, int maxLength) =>
        TryParse(value, maxLength, out var parsed, out var errors)
            ? parsed.Value
            : throw StringParsers<RefreshToken>.InvalidValue(errors);
}
