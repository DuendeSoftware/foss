// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement.Types;

[TypeConverter(typeof(StringValueConverter<ClientId>))]
[JsonConverter(typeof(StringValueJsonConverter<ClientId>))]
public record ClientId : IStringValue<ClientId>
{

    public static implicit operator ClientId(string value) => Parse(value);

    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(1024)
    ];

    private ClientId(string value) => Value = value;

    public string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out ClientId? parsed, out string[] errors) =>
        IStringValue<ClientId>.TryBuildValidatedObject(value, Validators, out parsed, out errors);


    public static ClientId Load(string result) => new(result);

    public static ClientId Parse(string value) => StringParsers<ClientId>.Parse(value);

    public static ClientId? ParseOrDefault(string? value) => StringParsers<ClientId>.ParseOrDefault(value);

}
