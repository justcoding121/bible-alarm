#nullable enable

using System.Reflection;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Tests.Support;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Bible.Alarm.Tests;

public sealed class ExceptionHandlingServiceTests
{
    private sealed class ListSink(List<LogEvent> events) : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => events.Add(logEvent);
    }

    [Fact]
    public void Dispose_without_setup_is_idempotent()
    {
        var sut = new ExceptionHandlingService(TestLogging.CreateLogger());

        sut.Dispose();
        sut.Dispose();
    }

    [Fact]
    public void Setup_then_dispose_unregisters_handlers_without_throw()
    {
        var sut = new ExceptionHandlingService(TestLogging.CreateLogger());

        sut.SetupGlobalExceptionHandlers();
        sut.Dispose();
        sut.Dispose();
    }

    [Fact]
    public void UnobservedTaskExceptionHandler_logs_and_flushes()
    {
        var events = new List<LogEvent>();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new ListSink(events))
            .CreateLogger();

        var sut = new ExceptionHandlingService(logger);
        sut.SetupGlobalExceptionHandlers();

        var method = typeof(ExceptionHandlingService).GetMethod(
            "UnobservedTaskExceptionHandler",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var args = new UnobservedTaskExceptionEventArgs(
            new AggregateException(new InvalidOperationException("task-fail")));

        method!.Invoke(sut, [null, args]);

        Assert.Contains(events, e => e.Level == LogEventLevel.Error);
    }

    [Fact]
    public void UnhandledExceptionHandler_logs_and_flushes()
    {
        var events = new List<LogEvent>();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new ListSink(events))
            .CreateLogger();

        var sut = new ExceptionHandlingService(logger);
        sut.SetupGlobalExceptionHandlers();

        var method = typeof(ExceptionHandlingService).GetMethod(
            "UnhandledExceptionHandler",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var args = new UnhandledExceptionEventArgs(new InvalidOperationException("fatal"), isTerminating: true);

        method!.Invoke(sut, [this, args]);

        Assert.Contains(events, e => e.Level == LogEventLevel.Error);
    }

    [Fact]
    public void FlushAndDelay_swallows_close_and_flush_errors()
    {
        var method = typeof(ExceptionHandlingService).GetMethod(
            "FlushAndDelay",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var prior = Log.Logger;
        try
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Sink(new ThrowingOnCloseSink())
                .CreateLogger();

            Assert.Null(Record.Exception(() => method!.Invoke(null, null)));
        }
        finally
        {
            Log.Logger = prior ?? new LoggerConfiguration().CreateLogger();
        }
    }

    private sealed class ThrowingOnCloseSink : ILogEventSink, IDisposable
    {
        public void Emit(LogEvent logEvent)
        {
        }

        public void Dispose() => throw new IOException("close failed for test");
    }
}

