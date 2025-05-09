// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement.Types;

[TypeConverter(typeof(StringValueConverter<ClientCredentialsClientName>))]
public record ClientCredentialsClientName : IStringValue<ClientCredentialsClientName>
{
    public static implicit operator ClientCredentialsClientName(string value) => Parse(value);
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(1024)
    ];

    private ClientCredentialsClientName(string value) => Value = value;

    public string Value { get; }


    public static bool TryParse(string value, [NotNullWhen(true)] out ClientCredentialsClientName? parsed, out string[] errors) =>
        IStringValue<ClientCredentialsClientName>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    public static ClientCredentialsClientName Parse(string value) => StringParsers<ClientCredentialsClientName>.Parse(value);

    static ClientCredentialsClientName IStringValue<ClientCredentialsClientName>.Load(string result) => new(result);

}
