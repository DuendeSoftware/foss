// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.RazorSlices;

internal sealed record SliceActivation(
    Action<RazorSlice, IServiceProvider?, HttpContext?>? Initialize,
    Func<RazorSlice>? CreateReplacement = null)
{
    internal static readonly SliceActivation Empty = new(Initialize: null);
}
