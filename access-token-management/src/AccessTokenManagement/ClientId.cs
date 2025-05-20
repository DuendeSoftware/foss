// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Represents an OIDC Client ID. This is a strongly typed value object that validates the string value.
/// </summary>
[TypeConverter(typeof(StringValueConverter<ClientId>))]
[JsonConverter(typeof(StringValueJsonConverter<ClientId>))]
public readonly record struct ClientId : IStronglyTypedValue<ClientId>
{
    public static implicit operator ClientId(string value) => Parse(value);
    public static implicit operator string(ClientId value) => value.ToString();

    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(1024)
    ];

    public ClientId() => throw new InvalidOperationException("Can't create null value");
    private ClientId(string value) => Value = value;

    private string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out ClientId? parsed, out string[] errors) =>
        IStronglyTypedValue<ClientId>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    static ClientId IStronglyTypedValue<ClientId>.Create(string result) => new(result);

    public static ClientId Parse(string value) => StringParsers<ClientId>.Parse(value);
}
