// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.RazorSlices;

/// <summary>Provides caller-owned helper instances for generated rendering facades.</summary>
public interface IHelperContext
{
    /// <summary>Throws when this context is no longer rendering.</summary>
    void EnsureActive();

    /// <summary>Gets one helper implementation per root render, shared by partials, layouts, and sections.</summary>
    TContract GetHelper<TContract, TImplementation>(Func<TImplementation> create)
        where TContract : class
        where TImplementation : RazorSliceHelpers;
}
