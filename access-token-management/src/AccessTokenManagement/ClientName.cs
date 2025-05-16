// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

/// <summary>
/// The name of a client in an OAuth flow. 
/// </summary>
[TypeConverter(typeof(StringValueConverter<ClientName>))]
public readonly record struct ClientName : IStronglyTypedString<ClientName>
{
    public static implicit operator ClientName(string value) => Parse(value);

    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(1024)
    ];

    public ClientName() => throw new InvalidOperationException("Can't create null value");
    private ClientName(string value) => Value = value;

    private string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out ClientName? parsed, out string[] errors) =>
        IStronglyTypedString<ClientName>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    public static ClientName Parse(string value) => StringParsers<ClientName>.Parse(value);

    static ClientName IStronglyTypedString<ClientName>.Create(string result) => new(result);
}
