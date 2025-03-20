// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache")
    .WithRedisCommander();

var idServer = builder.AddProject<Projects.Perf_IdentityServer>(Services.IdentityServer.ToString());

var tokenEndpoint = builder.AddProject<Projects.Perf_TokenEndpoint>(Services.TokenEndpoint.ToString())
    .WithReplicas(3)
    .WithReference(cache);
;

idServer.WithReference(tokenEndpoint);
tokenEndpoint.WithReference(idServer);



builder.Build().Run();
