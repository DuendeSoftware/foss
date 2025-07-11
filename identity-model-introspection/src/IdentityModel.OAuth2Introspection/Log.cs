// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection;

internal static class Log
{
    public static readonly Action<ILogger, Exception> NoExpClaimFound
        = LoggerMessage.Define(
            LogLevel.Warning,
            1,
            "No exp claim found on introspection response, can't cache");

    public static readonly Action<ILogger, DateTimeOffset, Exception> TokenExpiresOn
        = LoggerMessage.Define<DateTimeOffset>(
            LogLevel.Debug,
            2,
            "Token will expire on {Expiration}");

    public static readonly Action<ILogger, DateTimeOffset, Exception> SettingToCache
        = LoggerMessage.Define<DateTimeOffset>(
            LogLevel.Debug,
            3,
            "Setting cache item expiration to {Expiration}");

    public static readonly Action<ILogger, Exception> SkippingDotToken
        = LoggerMessage.Define(
            LogLevel.Trace,
            4,
            "Token contains a dot - skipped because SkipTokensWithDots is set");

    public static readonly Action<ILogger, Exception> TokenNotCached
        = LoggerMessage.Define(
            LogLevel.Trace,
            5,
            "Token is not cached");

    public static readonly Action<ILogger, string, Exception> IntrospectionError
        = LoggerMessage.Define<string>(
            LogLevel.Error,
            6,
            "Error returned from introspection endpoint: {Error}");
}
