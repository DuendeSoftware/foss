// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement.Internal;

internal sealed class AsyncLazy<T>(Func<Task<T>> taskFactory)
{
    private readonly Lock _initializationLock = new();
    private Task<T>? _task;

    public Task<T> Value
    {
        get
        {
            lock (_initializationLock)
            {
                // Share in-flight and successful results, but allow failed initialization to be retried.
                if (_task is null || _task.IsFaulted || _task.IsCanceled)
                {
                    _task = Task.Run(taskFactory);
                }

                return _task;
            }
        }
    }
}
