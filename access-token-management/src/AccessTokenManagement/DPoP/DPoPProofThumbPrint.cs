// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AccessTokenManagement.DPoP;

/// <summary>
/// Captures a dpop proof thumbprint. 
/// </summary>
public record DPoPProofThumbPrint : IStringValue<DPoPProofThumbPrint>
{
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [

        // The thumbprint is typically a sha hash, which should be max 44 characters.
        // Limiting it to 255 seems safe and reasonable. 
        ValidationRules.MaxLength(255),
    ];

    private DPoPProofThumbPrint(string value) => Value = value;

    public string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out DPoPProofThumbPrint? parsed, out string[] errors) =>
        IStringValue<DPoPProofThumbPrint>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    public static DPoPProofThumbPrint FromJsonWebKey(JsonWebKey jsonWebKey)
    {
        var value = Base64UrlEncoder.Encode(jsonWebKey.ComputeJwkThumbprint());
        return Parse(value);
    }

    public static DPoPProofThumbPrint Load(string result) => new(result);

    public static DPoPProofThumbPrint Parse(string value) => StringParsers<DPoPProofThumbPrint>.Parse(value);

}
