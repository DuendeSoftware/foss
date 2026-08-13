// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Represents a DPoP nonce.
/// A DPoP nonce is an opaque server-generated value sent to the client
/// that must be included in subsequent DPoP proofs the client creates.
/// It helps protect against replay and pre-generation attacks by ensuring that each DPoP
/// proof is freshly generated.
/// </summary>
public readonly record struct DPoPNonce : IStronglyTypedValue<DPoPNonce>
{
    /// <summary>
    /// Returns the wrapped nonce string.
    /// </summary>
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators =
    [
        // There is no strict limit for nonces, but given that headers can't typically be greater than 4k, this
        // seems reasonable.
        ValidationRules.MaxLength(4 * 1024),
    ];

    /// <summary>
    /// Prevents creating an uninitialized <see cref="DPoPNonce"/> instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">Always thrown.</exception>
    public DPoPNonce() => throw new InvalidOperationException("Can't create null value");

    private DPoPNonce(string value) => Value = value;

    private string Value { get; }

    /// <summary>
    /// Attempts to parse and validate a nonce string.
    /// </summary>
    /// <param name="value">The nonce string to parse.</param>
    /// <param name="parsed">The parsed <see cref="DPoPNonce"/> when parsing succeeds.</param>
    /// <param name="errors">Validation errors when parsing fails.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string value, [NotNullWhen(true)] out DPoPNonce? parsed, out string[] errors) =>
        IStronglyTypedValue<DPoPNonce>.TryBuildValidatedObject(value, Validators, out parsed, out errors);


    static DPoPNonce IStronglyTypedValue<DPoPNonce>.Create(string result) => new(result);

    /// <summary>
    /// Parses and validates a nonce string.
    /// </summary>
    /// <param name="value">The nonce string to parse.</param>
    /// <returns>The parsed <see cref="DPoPNonce"/>.</returns>
    public static DPoPNonce Parse(string value) => StringParsers<DPoPNonce>.Parse(value);

    /// <summary>
    /// Parses and validates a nonce string, returning <see langword="null"/> when the input is null or whitespace.
    /// </summary>
    /// <param name="value">The nonce string to parse.</param>
    /// <returns>The parsed <see cref="DPoPNonce"/>, or <see langword="null"/>.</returns>
    public static DPoPNonce? ParseOrDefault(string? value) => StringParsers<DPoPNonce>.ParseOrDefault(value);
}
