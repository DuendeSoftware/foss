// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Duende.AccessTokenManagement.Internal;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Represents the JSON Web Key (JWK) used to sign <see cref="DPoPProof"/> values.
/// </summary>
/// <remarks>
/// A proof key is the long-lived key material that binds issued tokens to a client.
/// A per-request <see cref="DPoPProof"/> is generated from this key by <see cref="IDPoPProofService"/>.
/// </remarks>
[TypeConverter(typeof(StringValueConverter<DPoPProofKey>))]
[JsonConverter(typeof(StringValueJsonConverter<DPoPProofKey>))]
public readonly record struct DPoPProofKey : IStronglyTypedValue<DPoPProofKey>
{
    /// <summary>
    /// Determines whether this proof key value equals another proof key value.
    /// </summary>
    /// <param name="other">The other value to compare.</param>
    /// <returns><see langword="true"/> when both values are equal; otherwise <see langword="false"/>.</returns>
    public bool Equals(DPoPProofKey other) => Value == other.Value;

    /// <summary>
    /// Returns the hash code for the wrapped key string.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Convenience method for converting a <see cref="DPoPProofKey"/> into a string.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator string(DPoPProofKey value) => value.ToString();

    private readonly JsonWebKey _jsonWebKey;

    /// <summary>
    /// Returns the wrapped JWK string.
    /// </summary>
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        // Officially, there's no max length for JWTs, but 32k is a good limit
        ValidationRules.MaxLength(32 * 1024),
        IsValidJsonWebKey()
    ];

    private static ValidationRule<string> IsValidJsonWebKey() =>
        (string value, out string message) =>
        {
            message = string.Empty;
            try
            {
                JsonWebKey.Create(value);
                return true;
            }
            catch (Exception e)
            {
                message = "String is not a valid json web key: " + e.Message;
                return false;
            }
        };

    /// <summary>
    /// Prevents creating an uninitialized <see cref="DPoPProofKey"/> instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">Always thrown.</exception>
    public DPoPProofKey() => throw new InvalidOperationException("Can't create null value");
    private DPoPProofKey(string value)
    {
        Value = value;
        _jsonWebKey = JsonWebKey.Create(value);
    }

    private string Value { get; }

    /// <summary>
    /// Converts the proof key into a <see cref="JsonWebKey"/>. 
    /// </summary>
    /// <returns></returns>
    public JsonWebKey ToJsonWebKey() => _jsonWebKey;

    /// <summary>
    /// Parses a value to a <see cref="DPoPProofKey"/>. This method will return false if the value is invalid
    /// and also includes a list of errors. This is useful for validating user input or other scenarios where you want to provide feedback
    /// </summary>
    public static bool TryParse(string value, [NotNullWhen(true)] out DPoPProofKey? parsed, out string[] errors) =>
        IStronglyTypedValue<DPoPProofKey>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    static DPoPProofKey IStronglyTypedValue<DPoPProofKey>.Create(string result) => new(result);

    /// <summary>
    /// Parses a value to a <see cref="DPoPProofKey"/>. This will throw an exception if the string is not valid.
    /// </summary>
    public static DPoPProofKey Parse(string value) => StringParsers<DPoPProofKey>.Parse(value);

    /// <summary>
    /// Parses a value to a <see cref="DPoPProofKey"/>. This will return null if the provided string
    /// is null or whitespace. This is a convenience method for when you want to parse a value that may
    /// contain null or whitespace strings. 
    /// </summary>
    public static DPoPProofKey? ParseOrDefault(string? value) => StringParsers<DPoPProofKey>.ParseOrDefault(value);

}
