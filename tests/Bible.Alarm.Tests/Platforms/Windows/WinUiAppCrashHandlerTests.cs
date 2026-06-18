#nullable enable

using System.Reflection;
using Bible.Alarm.Tests.Support;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using WinUiApp = Bible.Alarm.WinUI.App;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
public sealed class WinUiAppCrashHandlerTests
{
    private static readonly Type AppType = typeof(WinUiApp);

    private sealed class ListSink(List<LogEvent> events) : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => events.Add(logEvent);
    }

    private static MethodInfo? GetStaticMethod(string name) =>
        AppType.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);

    [Fact]
    public void WinUi_app_type_is_the_windows_entry_point()
    {
        Assert.Equal("Bible.Alarm.WinUI.App", AppType.FullName);
    }

    [Fact]
    public void UnobservedTaskExceptionHandler_logs_and_flushes()
    {
        var events = new List<LogEvent>();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new ListSink(events))
            .CreateLogger();

        var method = GetStaticMethod("UnobservedTaskExceptionHandler");
        Assert.NotNull(method);

        var args = new UnobservedTaskExceptionEventArgs(
            new AggregateException(new InvalidOperationException("task-fail")));

        Assert.Null(Record.Exception(() => method!.Invoke(null, [null, args])));
        Assert.Contains(events, e => e.Level == LogEventLevel.Error);
    }

    [Fact]
    public void UnhandledExceptionHandler_logs_exception_object()
    {
        var events = new List<LogEvent>();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new ListSink(events))
            .CreateLogger();

        var method = GetStaticMethod("UnhandledExceptionHandler");
        Assert.NotNull(method);

        var args = new UnhandledExceptionEventArgs(new InvalidOperationException("fatal"), isTerminating: true);

        Assert.Null(Record.Exception(() => method!.Invoke(null, [this, args])));
        Assert.Contains(events, e => e.Level == LogEventLevel.Fatal);
    }

    [Fact]
    public void UnhandledExceptionHandler_logs_non_exception_object()
    {
        var events = new List<LogEvent>();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new ListSink(events))
            .CreateLogger();

        var method = GetStaticMethod("UnhandledExceptionHandler");
        Assert.NotNull(method);

        var args = new UnhandledExceptionEventArgs("non-exception payload", isTerminating: false);

        Assert.Null(Record.Exception(() => method!.Invoke(null, [this, args])));
        Assert.Contains(events, e => e.Level == LogEventLevel.Fatal);
    }

    [Fact]
    public void FlushAndDelay_swallows_close_and_flush_errors()
    {
        var method = GetStaticMethod("FlushAndDelay");
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
            Log.Logger = prior ?? TestLogging.CreateLogger();
        }
    }

    [Fact]
    public void HandleMediaControlAction_invokes_without_throw_for_media_actions()
    {
        var method = GetStaticMethod("HandleMediaControlAction");
        Assert.NotNull(method);

        foreach (var argument in new[] { "action=next", "action=previous", "action=play", "action=pause" })
        {
            Assert.Null(Record.Exception(() => method!.Invoke(null, [argument])));
        }
    }

    [Fact]
    public void HandleActivation_parses_schedule_id_arguments_without_throw()
    {
        var method = GetStaticMethod("HandleActivation");
        Assert.NotNull(method);

        foreach (var argument in new[] { "42", "scheduleId=42" })
        {
            Assert.Null(Record.Exception(() => method!.Invoke(null, [argument])));
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
