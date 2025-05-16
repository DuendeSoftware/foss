// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement.DPoP;

public readonly record struct DPoPProofString : IStronglyTypedString<DPoPProofString>
{
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [

        // There is no official limit to the size of this. Use 4K as a reasonable limit.
        ValidationRules.MaxLength(4 * 1024),
    ];

    public DPoPProofString() => throw new InvalidOperationException("Can't create null value");
    public static implicit operator DPoPProofString(string value) => Parse(value);
    public static implicit operator string(DPoPProofString value) => value.ToString();
    private DPoPProofString(string value) => Value = value;

    private string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out DPoPProofString? parsed, out string[] errors) =>
        IStronglyTypedString<DPoPProofString>.TryBuildValidatedObject(value, Validators, out parsed, out errors);


    static DPoPProofString IStronglyTypedString<DPoPProofString>.Create(string result) => new(result);

    public static DPoPProofString Parse(string value) => StringParsers<DPoPProofString>.Parse(value);

}
