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
    /// <summary>
    /// Convenience method for converting a <see cref="ClientId"/> into a string.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator string(ClientId value) => value.ToString();

    /// <summary>
    /// Returns the wrapped client identifier string.
    /// </summary>
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(1024)
    ];

    /// <summary>
    /// Prevents creating an uninitialized <see cref="ClientId"/> instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">Always thrown.</exception>
    public ClientId() => throw new InvalidOperationException("Can't create null value");
    private ClientId(string value) => Value = value;

    private string Value { get; }

    /// <summary>
    /// Attempts to parse and validate a client identifier string.
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <param name="parsed">The parsed <see cref="ClientId"/> when parsing succeeds.</param>
    /// <param name="errors">Validation errors when parsing fails.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string value, [NotNullWhen(true)] out ClientId? parsed, out string[] errors) =>
        IStronglyTypedValue<ClientId>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    static ClientId IStronglyTypedValue<ClientId>.Create(string result) => new(result);

    /// <summary>
    /// Parses and validates a client identifier string.
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <returns>The parsed <see cref="ClientId"/>.</returns>
    public static ClientId Parse(string value) => StringParsers<ClientId>.Parse(value);
}
