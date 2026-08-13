// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Represents the JWK thumbprint for a <see cref="DPoPProofKey"/>.
/// </summary>
public readonly record struct DPoPProofThumbprint : IStronglyTypedValue<DPoPProofThumbprint>
{
    /// <summary>
    /// Returns the wrapped thumbprint string.
    /// </summary>
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [

        // The thumbprint is typically a sha hash, which should be max 44 characters.
        // Limiting it to 255 seems safe and reasonable.
        ValidationRules.MaxLength(255),
    ];

    /// <summary>
    /// Prevents creating an uninitialized <see cref="DPoPProofThumbprint"/> instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">Always thrown.</exception>
    public DPoPProofThumbprint() => throw new InvalidOperationException("Can't create null value");

    private DPoPProofThumbprint(string value) => Value = value;

    private string Value { get; }

    /// <summary>
    /// Attempts to parse and validate a thumbprint string.
    /// </summary>
    /// <param name="value">The thumbprint string to parse.</param>
    /// <param name="parsed">The parsed value when parsing succeeds.</param>
    /// <param name="errors">Validation errors when parsing fails.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string value, [NotNullWhen(true)] out DPoPProofThumbprint? parsed, out string[] errors) =>
        IStronglyTypedValue<DPoPProofThumbprint>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    /// <summary>
    /// Computes a thumbprint from a JSON Web Key.
    /// </summary>
    /// <param name="jsonWebKey">The key to compute the thumbprint from.</param>
    /// <returns>The computed <see cref="DPoPProofThumbprint"/> value.</returns>
    public static DPoPProofThumbprint FromJsonWebKey(JsonWebKey jsonWebKey)
    {
        var value = Base64UrlEncoder.Encode(jsonWebKey.ComputeJwkThumbprint());
        return Parse(value);
    }

    static DPoPProofThumbprint IStronglyTypedValue<DPoPProofThumbprint>.Create(string result) => new(result);

    /// <summary>
    /// Parses and validates a thumbprint string.
    /// </summary>
    /// <param name="value">The thumbprint string to parse.</param>
    /// <returns>The parsed <see cref="DPoPProofThumbprint"/>.</returns>
    public static DPoPProofThumbprint Parse(string value) => StringParsers<DPoPProofThumbprint>.Parse(value);

}
