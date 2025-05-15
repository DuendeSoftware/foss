// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Represents a dpop nonce.
/// In OAuth 2.0 with Demonstration of Proof-of-Possession(DPoP), a
/// DPoP nonce is a random, server-generated value sent to the client
/// that must be included in the next DPoP proof the client creates.
/// It helps protect against replay attacks by ensuring that each DPoP
/// proof is freshly generated and tied to a specific point in the protocol flow.
/// </summary>
public readonly record struct DPoPNonce : IStronglyTypedString<DPoPNonce>
{
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators =
    [
        // There is no strict limit for nonces, but given that headers can't typically be greater than 4k, this
        // seems reasonable. 
        ValidationRules.MaxLength(4 * 1024),
    ];

    public DPoPNonce() => throw new InvalidOperationException("Can't create null value");

    private DPoPNonce(string value) => Value = value;

    private string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out DPoPNonce? parsed, out string[] errors) =>
        IStronglyTypedString<DPoPNonce>.TryBuildValidatedObject(value, Validators, out parsed, out errors);


    static DPoPNonce IStronglyTypedString<DPoPNonce>.Create(string result) => new(result);

    public static DPoPNonce Parse(string value) => StringParsers<DPoPNonce>.Parse(value);
    public static DPoPNonce? ParseOrDefault(string? value) => StringParsers<DPoPNonce>.ParseOrDefault(value);
}
