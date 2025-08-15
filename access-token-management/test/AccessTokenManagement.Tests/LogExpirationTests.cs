// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.Framework;
using Microsoft.Extensions.Logging;

namespace Duende.AccessTokenManagement;

public class LogExpirationTests
{
    /// <summary>
    /// This test proves that the source generator for log messages do not invoke the function
    /// passed as an argument. 
    /// </summary>
    [Fact]
    public void Logging_using_a_function_will_not_invoke_function()
    {
        var loggerProvider = new TestLoggerProvider(_ => { }, "test");
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(loggerProvider));

        var logger = loggerFactory.CreateLogger("some");

        logger.IsEnabled(LogLevel.Trace);
        {
            logger.LogWithFunction(() => "foo");
        }

        loggerProvider.LogEntries.Any(x => x.Contains("foo")).ShouldBeFalse();
        loggerProvider.LogEntries.Any(x => x.Contains("System.Func")).ShouldBeTrue();
    }

    /// <summary>
    /// Just a sanity check that log messages should be written. 
    /// </summary>
    [Fact]
    public void Log_using_string_will_write_output()
    {
        var loggerProvider = new TestLoggerProvider(_ => { }, "test");
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(loggerProvider));
        var logger = loggerFactory.CreateLogger("some");

        logger.LogResult("foo");

        loggerProvider.LogEntries.Any(x => x.Contains("foo")).ShouldBeTrue();
    }
}

public static partial class TestLoggers
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Loggin With function result {functionResult}")]
#pragma warning disable LOGGEN036
    public static partial void LogWithFunction(this ILogger logger, Func<string> functionResult);
#pragma warning restore LOGGEN036

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "LogResult: {result}")]
    public static partial void LogResult(this ILogger logger, string result);
}
