#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class BootstrapLogLineFormatterTests
{
    [Fact]
    public void FormatUtcDiagnosticLine_suffixes_body_with_platform_newline()
    {
        var line = BootstrapLogLineFormatter.FormatUtcDiagnosticLine(
            new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc),
            "payload-text");

        Assert.EndsWith(Environment.NewLine, line);
        Assert.Contains("payload-text", line);
    }
}
