// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Duende.AccessTokenManagement.OpenIdConnect;

internal static class StringExtensions
{
    [DebuggerStepThrough]
    public static bool IsMissing([NotNullWhen(false)] this string? value) => string.IsNullOrWhiteSpace(value);


}
