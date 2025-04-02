// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AccessTokenManagement.OpenIdConnect;

// This code is from the blazor documentation:
// https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection?view=aspnetcore-8.0#access-server-side-blazor-services-from-a-different-di-scope

/// <summary>
/// Provides access to scoped blazor services from non-blazor DI scopes, such as
/// scopes created using IHttpClientFactory.
/// </summary>
[Obsolete(Constants.AtmPublicSurfaceInternal, UrlFormat = Constants.AtmPublicSurfaceLink)]
public class CircuitServicesAccessor
{
    static readonly AsyncLocal<IServiceProvider> BlazorServices = new();

    internal IServiceProvider? Services
    {
        get => BlazorServices.Value;
        set => BlazorServices.Value = value!;
    }
}

internal class ServicesAccessorCircuitHandler(
    IServiceProvider services,
#pragma warning disable CS0618 // Type or member is obsolete
    CircuitServicesAccessor servicesAccessor)
#pragma warning restore CS0618 // Type or member is obsolete
    : CircuitHandler
{
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next)
    {
        return async context =>
        {
            servicesAccessor.Services = services;
            await next(context);
            servicesAccessor.Services = null;
        };
    }
}

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
