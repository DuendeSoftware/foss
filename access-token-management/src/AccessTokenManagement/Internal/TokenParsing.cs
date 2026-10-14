// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.Internal;

/// <summary>
/// Parses tokens against a configured maximum length. When a value is too long, the exception names the option
/// that controls the limit. The token value is never included in the message.
/// </summary>
internal static class TokenParsing
{
    internal static AccessToken ParseAccessToken(string value, int maxLength, string optionName) =>
        AccessToken.TryParse(value, maxLength, out var parsed, out var errors)
            ? parsed.Value
            : throw CreateException<AccessToken>(value, errors, maxLength, optionName);

    internal static RefreshToken ParseRefreshToken(string value, int maxLength, string optionName) =>
        RefreshToken.TryParse(value, maxLength, out var parsed, out var errors)
            ? parsed.Value
            : throw CreateException<RefreshToken>(value, errors, maxLength, optionName);

    internal static IdentityToken? ParseIdentityTokenOrDefault(string? value, int maxLength, string optionName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return IdentityToken.TryParse(value, maxLength, out var parsed, out var errors)
            ? parsed
            : throw CreateException<IdentityToken>(value, errors, maxLength, optionName);
    }

    private static InvalidOperationException CreateException<T>(string value, string[] errors, int maxLength, string optionName)
        where T : struct, IStronglyTypedValue<T>
    {
        var message = StringParsers<T>.InvalidValue(errors).Message;
        if (value.Length > maxLength)
        {
            message += $" To accept longer tokens, increase {optionName} (currently {maxLength}).";
        }

        return new InvalidOperationException(message);
    }
}
