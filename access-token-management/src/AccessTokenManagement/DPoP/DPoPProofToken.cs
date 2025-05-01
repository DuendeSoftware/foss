// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement.DPoP;

public record DPoPProofToken : IStringValue<DPoPProofToken>
{
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [

        // There is no official limit to the size of this. Use 4K as a reasonable limit.
        ValidationRules.MaxLength(4 * 1024),
    ];

    private DPoPProofToken(string value) => Value = value;

    public string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out DPoPProofToken? parsed, out string[] errors) =>
        IStringValue<DPoPProofToken>.TryBuildValidatedObject(value, Validators, out parsed, out errors);


    public static DPoPProofToken Load(string result) => new(result);

    public static DPoPProofToken Parse(string value) => StringParsers<DPoPProofToken>.Parse(value);

}
