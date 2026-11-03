// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.RazorSlices;

/// <summary>A generated factory for a minimal slice without a model.</summary>
public interface IRazorSliceBaseProxy
{
    /// <summary>Creates a minimal slice.</summary>
    static abstract RazorSliceBase CreateSlice();
}

/// <summary>A generated factory for a minimal slice with a model.</summary>
/// <typeparam name="TModel">The model type.</typeparam>
public interface IRazorSliceBaseProxy<TModel>
{
    /// <summary>Creates a minimal slice with the specified model.</summary>
    static abstract RazorSliceBase<TModel> CreateSlice(TModel model);
}
