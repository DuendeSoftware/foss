// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Threading.Channels;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Util;

internal sealed class BlockingIntrospectionEndpoint() : IntrospectionEndpointHandler(Behavior.Active)
{
    private readonly Channel<IntrospectionCall> _arrivals = Channel.CreateUnbounded<IntrospectionCall>();
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public Task<IntrospectionCall> NextCall() =>
        _arrivals.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri!.AbsoluteUri.Contains("well-known"))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        Interlocked.Increment(ref _calls);
        var call = new IntrospectionCall();
        _arrivals.Writer.TryWrite(call);

        await call.Outcome.WaitAsync(cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}

internal sealed class IntrospectionCall
{
    private readonly TaskCompletionSource _outcome = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task Outcome => _outcome.Task;

    public void Succeed() => _outcome.SetResult();

    // This is how HttpClient reports its own timeout.
    public void TimeOut(string message) =>
        _outcome.SetException(new TaskCanceledException(message, new TimeoutException()));
}
