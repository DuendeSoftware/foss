// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Represents an OAuth resource parameter value.
/// </summary>
[TypeConverter(typeof(StringValueConverter<Resource>))]
public readonly record struct Resource : IStronglyTypedValue<Resource>
{
    /// <summary>
    /// The maximum supported length for a resource string.
    /// </summary>
    public const int MaxLength = 1024;

    /// <summary>
    /// Convenience method for converting a <see cref="Resource"/> into a string.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator string(Resource value) => value.ToString();

    /// <summary>
    /// Returns the wrapped resource string.
    /// </summary>
    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(MaxLength),
    ];

    /// <summary>
    /// You can't directly create this type. 
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public Resource() => throw new InvalidOperationException("Can't create null value");

    private Resource(string value) => Value = value;

    private string Value { get; }

    /// <summary>
    /// Parses a value to a <see cref="Resource"/>. This method will return false if the value is invalid
    /// and also includes a list of errors. This is useful for validating user input or other scenarios where you want to provide feedback
    /// </summary>
    public static bool TryParse(string value, [NotNullWhen(true)] out Resource? parsed, out string[] errors) =>
        IStronglyTypedValue<Resource>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    static Resource IStronglyTypedValue<Resource>.Create(string result) => new(result);

    /// <summary>
    /// Parses a value to a <see cref="Resource"/>. This will throw an exception if the string is not valid.
    /// </summary>
    public static Resource Parse(string value) => StringParsers<Resource>.Parse(value);
}
