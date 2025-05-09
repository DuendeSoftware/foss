// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement.Types;

[TypeConverter(typeof(StringValueConverter<Authority>))]
public record Authority : IStringValue<Authority>
{
    public static implicit operator Authority(string value) => Parse(value);

    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(4 * 1024),
        ValidationRules.Authority()
    ];

    private Authority(string value) => Value = value;

    public string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out Authority? parsed, out string[] errors) =>
        IStringValue<Authority>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    public static Authority Load(string result) => new(result);

    public static Authority Parse(string value) => StringParsers<Authority>.Parse(value);
    public static Authority? ParseOrDefault(string? value) => StringParsers<Authority>.ParseOrDefault(value);
}
