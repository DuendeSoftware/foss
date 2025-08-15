// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement.Framework;
public class TestLoggerProvider(WriteTestOutput writeOutput, string name) : ILoggerProvider
{
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly WriteTestOutput _writeOutput = writeOutput ?? throw new ArgumentNullException(nameof(writeOutput));
    private readonly string _name = name ?? throw new ArgumentNullException(nameof(name));

    private class DebugLogger(TestLoggerProvider parent, string category) : ILogger, IDisposable
    {
        public void Dispose()
        { }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => this;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var msg = $"[{logLevel}] {category} : {formatter(state, exception)}";
            parent.Log(msg);
        }
    }

    public List<string> LogEntries { get; } = new();

    private void Log(string msg)
    {
        try
        {
            var message = _watch.Elapsed.TotalMilliseconds.ToString("0000") + "ms - " + _name + msg;
#if NCRUNCH
            Console.WriteLine(message);
#else
            _writeOutput?.Invoke(message);
#endif
        }
        catch (Exception)
        {
            Console.WriteLine("Logging Failed: " + msg);
        }
        LogEntries.Add(msg);
    }

    public ILogger CreateLogger(string categoryName) => new DebugLogger(this, categoryName);

    public void Dispose()
    {
    }
}

public delegate void WriteTestOutput(string message);
