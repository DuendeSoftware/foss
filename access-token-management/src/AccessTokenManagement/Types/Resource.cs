// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement.Types;

[TypeConverter(typeof(StringValueConverter<Resource>))]
public record Resource : IStringValue<Resource>
{
    public static implicit operator Resource(string value) => Parse(value);
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(1024),
    ];

    private Resource(string value) => Value = value;

    public string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out Resource? parsed, out string[] errors) =>
        IStringValue<Resource>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    public static Resource Load(string result) => new(result);

    public static Resource Parse(string value) => StringParsers<Resource>.Parse(value);

    public static Resource? ParseOrDefault(string? value) => StringParsers<Resource>.ParseOrDefault(value);

}
