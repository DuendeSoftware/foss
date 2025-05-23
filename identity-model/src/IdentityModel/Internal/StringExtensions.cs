// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;

namespace Duende.IdentityModel.Internal;

internal static class StringExtensions
{
    [DebuggerStepThrough]
    public static bool IsMissing(this string? value) => string.IsNullOrWhiteSpace(value);

    [DebuggerStepThrough]
    public static bool IsPresent(this string? value) => !(value.IsMissing());

    [DebuggerStepThrough]
    public static string EnsureTrailingSlash(this string url)
    {
        if (!url.EndsWith("/"))
        {
            return url + "/";
        }

        return url;
    }

    [DebuggerStepThrough]
    public static string RemoveTrailingSlash(this string url)
    {
        if (url == null)
        {
            throw new ArgumentNullException(nameof(url));
        }

        if (url.EndsWith("/"))
        {
            url = url.Substring(0, url.Length - 1);
        }

        return url;
    }
}
