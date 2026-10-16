// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Util;

// The client stops waiting as soon as it cancels, and only sees a generic cancellation when the server throws one,
// so this is how a test sees when the server is done with a request and what it threw.
internal sealed class ServerRequests : IStartupFilter
{
    public const string HeaderName = "x-test-request";

    private readonly ConcurrentDictionary<string, TaskCompletionSource<Exception?>> _completed = new();

    public Task<Exception?> Completed(string name) => Get(name).Task.WaitAsync(TimeSpan.FromSeconds(10));

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            Exception? exception = null;
            try
            {
                await nextMiddleware();
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                if (context.Request.Headers.TryGetValue(HeaderName, out var name))
                {
                    Get(name.ToString()).TrySetResult(exception);
                }
            }
        });

        next(app);
    };

    private TaskCompletionSource<Exception?> Get(string name) =>
        _completed.GetOrAdd(name, _ => new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously));
}
