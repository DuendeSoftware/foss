// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement.Types;

[TypeConverter(typeof(StringValueConverter<ClientSecret>))]
public record ClientSecret : IStringValue<ClientSecret>
{

    public static implicit operator ClientSecret(string value) => Parse(value);

    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(1024)
    ];

    private ClientSecret(string value) => Value = value;

    public string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out ClientSecret? parsed, out string[] errors) =>
        IStringValue<ClientSecret>.TryBuildValidatedObject(value, Validators, out parsed, out errors);


    public static ClientSecret Load(string result) => new(result);

    public static ClientSecret Parse(string value) => StringParsers<ClientSecret>.Parse(value);

    public static ClientSecret? ParseOrDefault(string? value) => StringParsers<ClientSecret>.ParseOrDefault(value);
}
