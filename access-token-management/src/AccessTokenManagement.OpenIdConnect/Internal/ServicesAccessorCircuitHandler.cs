// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

internal class ServicesAccessorCircuitHandler(
    IServiceProvider services,
    CircuitServicesAccessor servicesAccessor)
    : CircuitHandler
{
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next) =>
        async context =>
        {
            servicesAccessor.Services = services;
            await next(context);
            servicesAccessor.Services = null;
        };
}
