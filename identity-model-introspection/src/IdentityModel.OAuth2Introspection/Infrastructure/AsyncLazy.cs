// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Infrastructure;

internal sealed class AsyncLazy<T>
{
    private Lazy<Task<T>> _lazyTaskFactory;
    private readonly Func<Task<T>> _taskFactory;
    private readonly object _lazyInitializationGuard = new object();

    public AsyncLazy(Func<Task<T>> taskFactory)
    {
        _taskFactory = taskFactory;
        _lazyTaskFactory = InitLazy(_taskFactory);
    }

    public Task<T> Value
    {
        get
        {
            //If the lazy value is not yet created, we should just return the lazy value (which will create it)
            //If the value has been created and the value (which is a Task<T>) is not faulted, we should just return the value;
            if (!(_lazyTaskFactory.IsValueCreated && _lazyTaskFactory.Value.IsFaulted))
            {
                return _lazyTaskFactory.Value;
            }

            lock (_lazyInitializationGuard)
            {
                if (_lazyTaskFactory.IsValueCreated && _lazyTaskFactory.Value.IsFaulted)
                {
                    _lazyTaskFactory = InitLazy(_taskFactory);
                }

                return _lazyTaskFactory.Value;
            }
        }
    }

    private static Lazy<Task<T>> InitLazy(Func<Task<T>> taskFactory) =>
        new Lazy<Task<T>>(() => Task.Run(taskFactory));
}
