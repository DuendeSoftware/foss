// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Cache key when caching client credential access tokens.
/// </summary>
public readonly record struct ClientCredentialsCacheKey : IStronglyTypedValue<ClientCredentialsCacheKey>
{
    /// <summary>
    /// Returns the wrapped cache key string.
    /// </summary>
    public override string ToString() => Value;

    /// <summary>
    /// The maximum supported length for a cache key value.
    /// </summary>
    public const int MaxLength = 1024;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(MaxLength)
    ];

    /// <summary>
    /// You can't directly create this type.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public ClientCredentialsCacheKey() => throw new InvalidOperationException("Can't create null value");
    private ClientCredentialsCacheKey(string value) => Value = value;

    private string Value { get; }

    /// <summary>
    /// Parses a value to a <see cref="ClientCredentialsCacheKey"/>. This will throw an exception if the string is not valid.
    /// </summary>
    public static ClientCredentialsCacheKey Parse(string value) => StringParsers<ClientCredentialsCacheKey>.Parse(value);

    /// <summary>
    /// Convenience method for converting a <see cref="ClientCredentialsCacheKey"/> into a string.
    /// </summary>
    /// <param name="key"></param>
    public static implicit operator string(ClientCredentialsCacheKey key) => key.Value;

    /// <summary>
    /// Parses a value to a <see cref="ClientCredentialsCacheKey"/>. This method will return false if the value is invalid
    /// and also includes a list of errors. This is useful for validating user input or other scenarios where you want to provide feedback
    /// </summary>
    public static bool TryParse(string value, [NotNullWhen(true)] out ClientCredentialsCacheKey? parsed, out string[] errors) =>
        IStronglyTypedValue<ClientCredentialsCacheKey>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    static ClientCredentialsCacheKey IStronglyTypedValue<ClientCredentialsCacheKey>.Create(string result) => new(result);
}
