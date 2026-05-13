#nullable enable

using Bible.Alarm.Platforms.Windows.Helpers;

namespace Bible.Alarm.Tests;

public sealed class WindowsBootstrapLoggerTests
{
    [Fact]
    public void WriteLine_swallows_exceptions_so_bootstrap_logging_never_faults_callers()
    {
        var ex = Record.Exception(() => WindowsBootstrapLogger.WriteLine("bootstrap diagnostics probe"));

        Assert.Null(ex);
    }

    [Fact]
    public void WriteException_swallows_exceptions_even_when_inner_logging_work_fails()
    {
        var ex = Record.Exception(() => WindowsBootstrapLogger.WriteException(new InvalidOperationException("probe")));

        Assert.Null(ex);
    }
}
