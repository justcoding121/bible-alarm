#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class BootstrapDiagnosticLogPathComposerTests
{
    [Fact]
    public void ComposeLogFilePath_nests_logs_directory_under_root_before_filename()
    {
        var expected = Path.Combine(@"C:\app-root", "logs", "bootstrap.txt");
        var actual = BootstrapDiagnosticLogPathComposer.ComposeLogFilePath(@"C:\app-root", "logs", "bootstrap.txt");

        Assert.Equal(expected, actual);
    }
}
