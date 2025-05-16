// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

[TypeConverter(typeof(StringValueConverter<Resource>))]
public readonly record struct Resource : IStronglyTypedString<Resource>
{
    public const int MaxLength = 1024;
    public static implicit operator Resource(string value) => Parse(value);
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(MaxLength),
    ];

    public Resource() => throw new InvalidOperationException("Can't create null value");

    private Resource(string value) => Value = value;

    private string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out Resource? parsed, out string[] errors) =>
        IStronglyTypedString<Resource>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    static Resource IStronglyTypedString<Resource>.Create(string result) => new(result);

    public static Resource Parse(string value) => StringParsers<Resource>.Parse(value);
}
