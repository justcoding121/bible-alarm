#nullable enable

using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Bible.Alarm.Tests.Support;

internal static class TestLogging
{
    public static Logger CreateLogger() =>
        new LoggerConfiguration()
            .WriteTo.Sink(new NopSink())
            .CreateLogger();

    private sealed class NopSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
        }
    }
}
