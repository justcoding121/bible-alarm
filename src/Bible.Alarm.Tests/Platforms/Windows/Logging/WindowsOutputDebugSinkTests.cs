#nullable enable

using Bible.Alarm.Platforms.Windows.Logging;
using Serilog;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
public sealed class WindowsOutputDebugSinkTests
{
    [Fact]
    public void Emit_writes_through_sink_without_throwing()
    {
        var sink = new WindowsOutputDebugSink();

        using var log = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(sink)
            .CreateLogger();

        log.Information("unit test {Marker}", "sink");
        log.Warning(new InvalidOperationException("x"), "with exception");
    }
}
