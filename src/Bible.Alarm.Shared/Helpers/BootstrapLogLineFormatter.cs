#nullable enable

using System;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// UTC-prefixed bootstrap log lines shared by early-boot diagnostics before Serilog wiring.
/// </summary>
public static class BootstrapLogLineFormatter
{
    public static string FormatUtcDiagnosticLine(DateTime utcTimestamp, string messageBody) =>
        $"{utcTimestamp:yyyy-MM-dd HH:mm:ss}Z {messageBody}{Environment.NewLine}";
}
