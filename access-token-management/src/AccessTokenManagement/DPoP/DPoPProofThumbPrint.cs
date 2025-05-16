// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Captures a dpop proof thumbprint.
/// </summary>
public readonly record struct DPoPProofThumbprint : IStronglyTypedString<DPoPProofThumbprint>
{
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [

        // The thumbprint is typically a sha hash, which should be max 44 characters.
        // Limiting it to 255 seems safe and reasonable.
        ValidationRules.MaxLength(255),
    ];

    public DPoPProofThumbprint() => throw new InvalidOperationException("Can't create null value");

    private DPoPProofThumbprint(string value) => Value = value;

    private string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out DPoPProofThumbprint? parsed, out string[] errors) =>
        IStronglyTypedString<DPoPProofThumbprint>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    public static DPoPProofThumbprint FromJsonWebKey(JsonWebKey jsonWebKey)
    {
        var value = Base64UrlEncoder.Encode(jsonWebKey.ComputeJwkThumbprint());
        return Parse(value);
    }

    static DPoPProofThumbprint IStronglyTypedString<DPoPProofThumbprint>.Create(string result) => new(result);

    public static DPoPProofThumbprint Parse(string value) => StringParsers<DPoPProofThumbprint>.Parse(value);

}
