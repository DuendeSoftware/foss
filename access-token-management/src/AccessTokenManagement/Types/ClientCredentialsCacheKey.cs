// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement.Types;

public record ClientCredentialsCacheKey : IStringValue<ClientCredentialsCacheKey>
{
    public override string ToString() => Value;

    public const int MaxLength = 255;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(MaxLength)
    ];

    private ClientCredentialsCacheKey(string value) => Value = value;

    public string Value { get; }
    public static ClientCredentialsCacheKey Parse(string value) => StringParsers<ClientCredentialsCacheKey>.Parse(value);

    public static bool TryParse(string value, [NotNullWhen(true)] out ClientCredentialsCacheKey? parsed, out string[] errors) =>
        IStringValue<ClientCredentialsCacheKey>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    static ClientCredentialsCacheKey IStringValue<ClientCredentialsCacheKey>.Load(string result) => new(result);
}
