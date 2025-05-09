// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

// This code is from the blazor documentation:
// https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection?view=aspnetcore-8.0#access-server-side-blazor-services-from-a-different-di-scope

internal static class CircuitServicesServiceCollectionExtensions
{
    public static IServiceCollection AddCircuitServicesAccessor(
        this IServiceCollection services)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        services.AddScoped<CircuitServicesAccessor>();
#pragma warning restore CS0618 // Type or member is obsolete
        services.AddScoped<CircuitHandler, ServicesAccessorCircuitHandler>();

        return services;
    }
}
