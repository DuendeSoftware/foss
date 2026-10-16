// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Util;

/// <summary>
/// Middleware that reports when the server has finished handling a request, so a test can tell that the
/// handler stopped even when the client gave up on the request first. Requests are named by the
/// <see cref="HeaderName"/> header.
/// </summary>
internal sealed class RequestCompletions : IStartupFilter
{
    public const string HeaderName = "x-test-request";

    private readonly ConcurrentDictionary<string, TaskCompletionSource> _completed = new();

    /// <summary>Completes once the server has finished handling the request with this name.</summary>
    public Task Completed(string name) => Get(name).Task;

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            try
            {
                await nextMiddleware();
            }
            finally
            {
                if (context.Request.Headers.TryGetValue(HeaderName, out var name))
                {
                    Get(name.ToString()).TrySetResult();
                }
            }
        });

        next(app);
    };

    private TaskCompletionSource Get(string name) =>
        _completed.GetOrAdd(name, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
}
