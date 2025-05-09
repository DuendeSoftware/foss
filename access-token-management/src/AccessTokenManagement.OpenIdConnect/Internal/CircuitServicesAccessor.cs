// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

/// <summary>
/// Provides access to scoped blazor services from non-blazor DI scopes, such as
/// scopes created using IHttpClientFactory.
/// </summary>
internal class CircuitServicesAccessor
{
    static readonly AsyncLocal<IServiceProvider> BlazorServices = new();

    internal IServiceProvider? Services
    {
        get => BlazorServices.Value;
        set => BlazorServices.Value = value!;
    }
}
