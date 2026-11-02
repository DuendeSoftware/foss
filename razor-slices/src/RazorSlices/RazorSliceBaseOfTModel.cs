// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.RazorSlices;

/// <summary>A minimal streaming Razor template with a strongly typed model.</summary>
/// <typeparam name="TModel">The model type.</typeparam>
public abstract class RazorSliceBase<TModel> : RazorSliceBase
{
    /// <summary>Gets or sets the model.</summary>
    public required TModel Model { get; set; }
}
