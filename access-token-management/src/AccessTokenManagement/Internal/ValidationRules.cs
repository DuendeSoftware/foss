// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace Duende.AccessTokenManagement.Internal;

internal static partial class ValidationRules
{
    public static ValidationRule<string> MaxLength(int maxLength) =>
        (string s, out string message) =>
        {
            var isValid = s.Length <= maxLength;
            message = !isValid ? $"The string exceeds maximum length {maxLength}." : string.Empty;

            return isValid;
        };

    [GeneratedRegex("^[a-zA-Z0-9]*$")]
    private static partial Regex AlphaNumericRegex();

    public static ValidationRule<string> AlphaNumeric() =>
        (string s, out string message) =>
        {
            var isValid = AlphaNumericRegex().IsMatch(s);
            message = !isValid ? "The string must be alphanumeric." : string.Empty;

            return isValid;
        };

    public static ValidationRule<string> Regex(Regex regex, string errorMessage) =>
        (string s, out string message) =>
        {
            var isValid = regex.IsMatch(s);
            message = !isValid ? errorMessage : string.Empty;

            return isValid;
        };

    public static ValidationRule<string> Uri() =>
        (string s, out string message) =>
        {
            var isValid = System.Uri.TryCreate(s, UriKind.Absolute, out var _);
            message = !isValid ? "The string must be a valid Uri." : string.Empty;

            return isValid;
        };

    public static ValidationRule<string> Authority() =>
        (string s, out string message) =>
        {
            var isValid = System.Uri.TryCreate(s, UriKind.Absolute, out var uri)
                          && uri is { IsDefaultPort: true, PathAndQuery: "/" };


            message = !isValid ? "The string must be a valid Authority." : string.Empty;

            return isValid;
        };
}
