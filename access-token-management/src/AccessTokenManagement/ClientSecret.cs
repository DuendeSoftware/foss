// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

[TypeConverter(typeof(StringValueConverter<ClientSecret>))]
public readonly record struct ClientSecret : IStronglyTypedString<ClientSecret>
{

    public static implicit operator ClientSecret(string value) => Parse(value);

    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(1024)
    ];

    public ClientSecret() => throw new InvalidOperationException("Can't create null value");
    private ClientSecret(string value) => Value = value;

    private string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out ClientSecret? parsed, out string[] errors) =>
        IStronglyTypedString<ClientSecret>.TryBuildValidatedObject(value, Validators, out parsed, out errors);


    static ClientSecret IStronglyTypedString<ClientSecret>.Create(string result) => new(result);

    public static ClientSecret Parse(string value) => StringParsers<ClientSecret>.Parse(value);
}
