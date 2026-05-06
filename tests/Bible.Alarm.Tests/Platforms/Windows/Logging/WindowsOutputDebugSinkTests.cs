#nullable enable

using Bible.Alarm.Platforms.Windows.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Parsing;

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

    [Fact]
    public void Emit_maps_each_level_band_and_default_label_for_unknown_level()
    {
        var sink = new WindowsOutputDebugSink();

        using var log = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();

        log.Verbose("verbose");
        log.Debug("debug");
        log.Information("information");
        log.Warning("warning");
        log.Error("error");
        log.Fatal("fatal");

        var template = new MessageTemplateParser().Parse("orphan band");
        var orphanBand = new LogEvent(
            DateTimeOffset.UtcNow,
            (LogEventLevel)42,
            exception: null,
            messageTemplate: template,
            properties: Array.Empty<LogEventProperty>());
        sink.Emit(orphanBand);
    }
}
