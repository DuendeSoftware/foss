// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.Tests;

public class TestOptionsMonitor<TOptions>(TOptions? currentValue = null) : IOptionsMonitor<TOptions>
    where TOptions : class, new()
{
    public TOptions CurrentValue { get; set; } = currentValue ?? new();

    public TOptions Get(string? name)
    {
        return CurrentValue;
    }

    public IDisposable? OnChange(Action<TOptions, string?> listener)
    {
        throw new NotImplementedException();
    }
}
