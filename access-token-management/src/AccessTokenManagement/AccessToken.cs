// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Strongly typed representation of an access token. This can be a Client Credentials or User token.
/// </summary>
[JsonConverter(typeof(StringValueJsonConverter<AccessToken>))]
public readonly record struct AccessToken : IStronglyTypedValue<AccessToken>
{
    /// <summary>
    /// Returns the wrapped access token string.
    /// </summary>
    public override string ToString() => Value;

    /// <summary>
    /// The default maximum supported length for an access token string. It is used when no explicit maximum is supplied.
    /// ATM read paths use the configured max length option instead:
    /// ClientCredentialsTokenManagementOptions.MaxAccessTokenLength for client credentials tokens and
    /// UserTokenManagementOptions.MaxAccessTokenLength for user tokens.
    /// </summary>
    // Officially, there's no max length for JWTs, but 32k is a good limit.
    public const int MaxLength = 32 * 1024; // 32k

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(MaxLength)
    ];

    /// <summary>
    /// Convenience method for converting a <see cref="AccessToken"/> into a string.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator string(AccessToken value) => value.ToString();

    /// <summary>
    /// You can't directly create this type.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public AccessToken() => throw new InvalidOperationException("Can't create null value");
    private AccessToken(string value) => Value = value;

    private string Value { get; }

    /// <summary>
    /// Parses a value to a <see cref="AccessToken"/>. This method will return false if the value is invalid
    /// and also includes a list of errors. This is useful for validating user input or other scenarios where you want to provide feedback
    /// </summary>
    public static bool TryParse(string value, [NotNullWhen(true)] out AccessToken? parsed, out string[] errors) =>
        TryParse(value, MaxLength, out parsed, out errors);

    /// <summary>
    /// Parses a value to a <see cref="AccessToken"/> using the supplied maximum length instead of <see cref="MaxLength"/>.
    /// This method will return false if the value is invalid and also includes a list of errors. It lets code outside
    /// ATM (custom <c>IUserTokenStore</c> implementations, other Duende libraries) honor a configured limit.
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <param name="maxLength">The maximum accepted length. Must be greater than zero.</param>
    /// <param name="parsed">The parsed value, if parsing succeeded.</param>
    /// <param name="errors">The validation errors, if parsing failed.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is zero or negative.</exception>
    public static bool TryParse(string value, int maxLength, [NotNullWhen(true)] out AccessToken? parsed, out string[] errors)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);
        return IStronglyTypedValue<AccessToken>.TryBuildValidatedObject(
            value,
            maxLength == MaxLength ? Validators : [ValidationRules.MaxLength(maxLength)],
            out parsed,
            out errors);
    }

    static AccessToken IStronglyTypedValue<AccessToken>.Create(string result) => new(result);

    /// <summary>
    /// Parses a value to a <see cref="AccessToken"/>. This will throw an exception if the string is not valid.
    /// </summary>
    public static AccessToken Parse(string value) => Parse(value, MaxLength);

    /// <summary>
    /// Parses a value to a <see cref="AccessToken"/> using the supplied maximum length instead of <see cref="MaxLength"/>.
    /// This will throw an exception if the string is not valid. It lets code outside ATM (custom
    /// <c>IUserTokenStore</c> implementations, other Duende libraries) honor a configured limit.
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <param name="maxLength">The maximum accepted length. Must be greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">The value is not valid.</exception>
    public static AccessToken Parse(string value, int maxLength) =>
        TryParse(value, maxLength, out var parsed, out var errors)
            ? parsed.Value
            : throw StringParsers<AccessToken>.InvalidValue(errors);
}
