// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

/// <summary>
/// The name of the token client, that's used to configure the openid connect
/// configuration in the client application. This may be, but doesn't have to be,
/// the same value as the <see cref="ClientId"/> which is used to identify the client in
/// the token endpoint.
/// </summary>
[TypeConverter(typeof(StringValueConverter<ClientCredentialsClientName>))]
public readonly record struct ClientCredentialsClientName : IStronglyTypedValue<ClientCredentialsClientName>
{
    /// <summary>
    /// Convenience method for converting a <see cref="ClientCredentialsClientName"/> into a string.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator string(ClientCredentialsClientName value) => value.ToString();

    /// <summary>
    /// Returns the wrapped client name string.
    /// </summary>
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(1024)
    ];

    /// <summary>
    /// You can't directly create this type. 
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public ClientCredentialsClientName() => throw new InvalidOperationException("Can't create null value");
    private ClientCredentialsClientName(string value) => Value = value;

    private string Value { get; }

    /// <summary>
    /// Parses a value to a <see cref="ClientCredentialsClientName"/>. This method will return false if the value is invalid
    /// and also includes a list of errors. This is useful for validating user input or other scenarios where you want to provide feedback
    /// </summary>
    public static bool TryParse(string value, [NotNullWhen(true)] out ClientCredentialsClientName? parsed, out string[] errors) =>
        IStronglyTypedValue<ClientCredentialsClientName>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    /// <summary>
    /// Parses a value to a <see cref="ClientCredentialsClientName"/>. This will throw an exception if the string is not valid.
    /// </summary>
    public static ClientCredentialsClientName Parse(string value) => StringParsers<ClientCredentialsClientName>.Parse(value);

    static ClientCredentialsClientName IStronglyTypedValue<ClientCredentialsClientName>.Create(string result) => new(result);
}
