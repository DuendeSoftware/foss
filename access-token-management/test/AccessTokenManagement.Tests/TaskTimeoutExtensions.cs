// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;

namespace Duende.AccessTokenManagement;

public static class TaskTimeoutExtensions
{
    private static TimeSpan IncreaseTimeoutIfDebuggerAttached(TimeSpan timeout)
    {
        // Wait a bit longer if the debugger is attached. This prevents timeouts during debugging.
        if (Debugger.IsAttached)
        {
            return TimeSpan.FromMinutes(10);
        }

        return timeout == default ? TimeSpan.FromSeconds(2) : timeout;
    }

    public static async Task ThrowOnTimeout(this Task task, TimeSpan timeout = default)
    {
        timeout = IncreaseTimeoutIfDebuggerAttached(timeout);

        using (var cts = new CancellationTokenSource())
        {
            var delayTask = Task.Delay(timeout, cts.Token);

            var resultTask = await Task.WhenAny(task, delayTask);
            if (resultTask == delayTask)
            {
                // Operation cancelled
                throw new OperationCanceledException();
            }

            cts.Cancel();

            await task;
        }
    }
}
