// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Represents an OAuth/OIDC client secret value.
/// </summary>
[TypeConverter(typeof(StringValueConverter<ClientSecret>))]
public readonly record struct ClientSecret : IStronglyTypedValue<ClientSecret>
{
    /// <summary>
    /// Convenience method for converting a <see cref="ClientSecret"/> into a string.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator string(ClientSecret value) => value.ToString();

    /// <summary>
    /// Returns the wrapped client secret string.
    /// </summary>
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(1024)
    ];

    /// <summary>
    /// You can't directly create this type. 
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public ClientSecret() => throw new InvalidOperationException("Can't create null value");

    private ClientSecret(string value) => Value = value;

    private string Value { get; }

    /// <summary>
    /// Parses a value to a <see cref="ClientSecret"/>. This method will return false if the value is invalid
    /// and also includes a list of errors. This is useful for validating user input or other scenarios where you want to provide feedback
    /// </summary>
    public static bool TryParse(string value, [NotNullWhen(true)] out ClientSecret? parsed, out string[] errors) =>
        IStronglyTypedValue<ClientSecret>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    static ClientSecret IStronglyTypedValue<ClientSecret>.Create(string result) => new(result);

    /// <summary>
    /// Parses a value to a <see cref="ClientSecret"/>. This will throw an exception if the string is not valid.
    /// </summary>
    public static ClientSecret Parse(string value) => StringParsers<ClientSecret>.Parse(value);
}
