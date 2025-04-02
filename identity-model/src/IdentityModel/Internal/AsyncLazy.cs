// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.IdentityModel.Internal;

internal class AsyncLazy<T> : Lazy<Task<T>>
{
    public AsyncLazy(Func<Task<T>> taskFactory) :
        base(() => GetTaskAsync(taskFactory).Unwrap())
    { }

    private static async Task<Task<T>> GetTaskAsync(Func<Task<T>> taskFactory)
    {
        if (TaskHelpers.CanFactoryStartNew)
        {
            // Runs the task factory in a background thread and retrieves the resulting task.
            return Task<Task<T>>.Factory.StartNew(taskFactory).Unwrap();
        }
        else
        {
            // Let the task factory run synchronously in its own context.
            await Task.Yield();

            return taskFactory();
        }
    }
}
