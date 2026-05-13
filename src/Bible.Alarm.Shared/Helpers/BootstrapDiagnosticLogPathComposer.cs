#nullable enable

using System.IO;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Builds bootstrap diagnostic log paths under a resolved root directory (packaged cache or exe-relative).
/// </summary>
public static class BootstrapDiagnosticLogPathComposer
{
    public static string ComposeLogFilePath(string rootDirectory, string logsDirectoryName, string fileName) =>
        Path.Combine(Path.Combine(rootDirectory, logsDirectoryName), fileName);
}
