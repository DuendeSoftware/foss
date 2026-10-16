// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Util;

// When the server throws, the client only sees a generic error, so this is how a test sees what the server threw.
internal sealed class ServerRequests : IStartupFilter
{
    public const string HeaderName = "x-test-request";

    private readonly ConcurrentDictionary<string, TaskCompletionSource<Exception?>> _completed = new();

    public Task<Exception?> ExceptionThrownFor(string name) => Get(name).Task.WaitAsync(TimeSpan.FromSeconds(10));

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
