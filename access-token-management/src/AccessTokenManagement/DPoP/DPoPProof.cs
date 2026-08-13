// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Represents a single signed DPoP proof JWT value.
/// </summary>
/// <remarks>
/// This value is sent in the HTTP <c>DPoP</c> header for one request.
/// It is generated from a <see cref="DPoPProofRequest"/> by <see cref="IDPoPProofService"/>
/// using a <see cref="DPoPProofKey"/>.
/// </remarks>
public readonly record struct DPoPProof : IStronglyTypedValue<DPoPProof>
{
    /// <summary>
    /// Returns the string representation of the DPoP proof value.
    /// </summary>
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        // There is no official limit to the size of this. Use 4K as a reasonable limit.
        ValidationRules.MaxLength(4 * 1024),
    ];

    /// <summary>
    /// prevents calling constructors directly.
    /// </summary>
    public DPoPProof() => throw new InvalidOperationException("Can't create null value");

    /// <summary>
    /// Convenience method that Implicitly converts a DPoPProof to its string representation.
    /// </summary>
    /// <param name="value">The DPoPProof value to convert.</param>
    public static implicit operator string(DPoPProof value) => value.ToString();

    /// <summary>
    /// Initializes a new instance of the DPoPProof struct with the specified value.
    /// </summary>
    /// <param name="value">The DPoP proof string value.</param>
    private DPoPProof(string value) => Value = value;

    /// <summary>
    /// Gets the underlying string value of the DPoP proof.
    /// </summary>
    private string Value { get; }

    /// <summary>
    /// Attempts to parse and validate a string as a DPoPProof.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <param name="parsed">The resulting DPoPProof if parsing succeeds.</param>
    /// <param name="errors">Any validation errors encountered.</param>
    /// <returns>True if parsing and validation succeed; otherwise, false.</returns>
    public static bool TryParse(string value, [NotNullWhen(true)] out DPoPProof? parsed, out string[] errors) =>
        IStronglyTypedValue<DPoPProof>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    /// <summary>
    /// Creates a new DPoPProof instance from a string value without validation.
    /// </summary>
    /// <param name="result">The string value to wrap.</param>
    /// <returns>A new DPoPProof instance.</returns>
    static DPoPProof IStronglyTypedValue<DPoPProof>.Create(string result) => new(result);

    /// <summary>
    /// Parses a string into a DPoPProof, throwing an exception if validation fails.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <returns>The parsed DPoPProof instance.</returns>
    public static DPoPProof Parse(string value) => StringParsers<DPoPProof>.Parse(value);
}
