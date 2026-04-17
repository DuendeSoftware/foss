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
    private const int Kilobyte = 1024;

    public override string ToString() => Value;

    // Officially, there's no max length for JWTs, but keep construction bounded.
    // Runtime read boundaries apply the configurable limit.
    public const int MaxLength = 100 * Kilobyte;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(MaxLength)
    ];

    private static ValidationRule<string>[] BuildValidators(int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);
        return [ValidationRules.MaxLength(maxLength)];
    }

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
        IStronglyTypedValue<AccessToken>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    /// <summary>
    /// Parses a value to a <see cref="AccessToken"/> using the supplied maximum length.
    /// </summary>
    public static bool TryParse(string value, int maxLength, [NotNullWhen(true)] out AccessToken? parsed, out string[] errors) =>
        IStronglyTypedValue<AccessToken>.TryBuildValidatedObject(value, BuildValidators(maxLength), out parsed, out errors);

    static AccessToken IStronglyTypedValue<AccessToken>.Create(string result) => new(result);

    /// <summary>
    /// Parses a value to a <see cref="AccessToken"/>. This will throw an exception if the string is not valid.
    /// </summary>
    public static AccessToken Parse(string value) => StringParsers<AccessToken>.Parse(value);

    /// <summary>
    /// Parses a value to a <see cref="AccessToken"/> using the supplied maximum length.
    /// </summary>
    public static AccessToken Parse(string value, int maxLength)
    {
        if (TryParse(value, maxLength, out var parsed, out var errors))
        {
            return parsed.Value;
        }

        throw new InvalidOperationException(
            $"Received an invalid {nameof(AccessToken)}. Errors: {string.Join("", errors.Select(x => $"{Environment.NewLine}\t - {x}"))}");
    }
}
