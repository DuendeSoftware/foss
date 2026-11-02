// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.RazorSlices;

internal static class RazorSliceHelperActivation<TImplementation> where TImplementation : RazorSliceHelpers
{
    private static volatile Func<RazorSliceHelpers>? _replacement;

    static RazorSliceHelperActivation()
    {
        if (HotReloadService.IsSupported)
        {
            HotReloadService.ClearCacheEvent += Replace;
        }
    }

    internal static RazorSliceHelpers Create(Func<TImplementation> create) => _replacement is { } replacement ? replacement() : create();

    [UnconditionalSuppressMessage("Trimming", "IL2072",
        Justification = "Replacement types are used only during Hot Reload, not in trimmed applications.")]
    internal static void Replace(Type[]? changedTypes)
    {
        if (HotReloadService.TryGetUpdatedType(changedTypes, typeof(TImplementation), out var updated))
        {
            _replacement = () => (RazorSliceHelpers)Activator.CreateInstance(updated)!;
        }
    }
}
