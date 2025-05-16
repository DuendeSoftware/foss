// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.AccessTokenManagement.Internal;

/// <summary>
/// Interface for strongly typed objects that wrap a string value.
///
/// This makes sure all these objects have similar methods. Also, it provides
/// a generic way to build them. 
/// </summary>
/// <typeparam name="TSelf"></typeparam>
internal interface IStronglyTypedString<TSelf> where TSelf : struct, IStronglyTypedString<TSelf>
{
    /// <summary>
    /// Attempt to parse the value object from a string. Return a list of errors if it fails. 
    /// </summary>
    /// <param name="value">The value to parse</param>
    /// <param name="parsed">The parsed result</param>
    /// <param name="errors">Errors that occurred during parsing. </param>
    /// <returns>True if parsing was successful</returns>
    static abstract bool TryParse(string value, [NotNullWhen(true)] out TSelf? parsed, out string[] errors);

    /// <summary>
    /// Build an object that represents the string value WITHOUT validation. 
    /// </summary>
    /// <param name="result"></param>
    /// <returns>The build object</returns>
    internal static abstract TSelf Create(string result);

    /// <summary>
    /// Parse the value object from a string. This method throws if validation fails and should be done when the
    /// only course of action for invalid values is to throw anyway. 
    /// </summary>
    /// <param name="value">The value to parse</param>
    /// <returns>The parsed object</returns>
    public static abstract TSelf Parse(string value);

    /// <summary>
    /// Implements validation logic for an object. Also creates the object 
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <param name="validationRules">The validation rules (which are delegates) used to validate the value</param>
    /// <param name="parsed">The parsed object, if validation succeeded. </param>
    /// <param name="foundErrors">The errors that were found. </param>
    /// <returns>True if validation succeeded</returns>
    internal static bool TryBuildValidatedObject(string value, ValidationRule<string>[] validationRules, [NotNullWhen(true)] out TSelf? parsed, out string[] foundErrors)
    {
        parsed = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            foundErrors = ["The string cannot be null or empty."];
            return false;
        }

        List<string> errors = [];
        foreach (var validator in validationRules)
        {
            if (!validator(value, out var message))
            {
                errors.Add(message);
            }
        }

        if (!errors.Any())
        {
            parsed = TSelf.Create(value);
        }
        foundErrors = errors.ToArray();

        return !foundErrors.Any();
    }
}
