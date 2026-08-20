using System.IO;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Platforms.iOS.Helpers;

/// <summary>
/// Writes unhandled/unobserved exception details to Documents/logs/bootstrap.txt when Serilog may not have flushed.
/// Same pattern as Windows so crashes leave a trace on disk (e.g. for TestFlight builds).
/// </summary>
internal static class IOsBootstrapLogger
{
    public static void WriteException(Exception ex)
    {
        try
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var logDir = Path.Combine(basePath, AppConstants.FilePaths.LogsDirectoryName);
            Directory.CreateDirectory(logDir);
            var path = Path.Combine(logDir, AppConstants.FilePaths.BootstrapDiagnosticLogFileName);
            var text = ex.ToString();
            var line = BootstrapLogLineFormatter.FormatUtcDiagnosticLine(DateTime.UtcNow, $"Exception: {text}");
            File.AppendAllText(path, line);
        }
        catch (Exception)
        {
            // Must not throw
        }
    }

    public static void WriteLine(string message)
    {
        try
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var logDir = Path.Combine(basePath, AppConstants.FilePaths.LogsDirectoryName);
            Directory.CreateDirectory(logDir);
            var path = Path.Combine(logDir, AppConstants.FilePaths.BootstrapDiagnosticLogFileName);
            var line = BootstrapLogLineFormatter.FormatUtcDiagnosticLine(DateTime.UtcNow, message);
            File.AppendAllText(path, line);
        }
        catch (Exception)
        {
            // Must not throw
        }
    }
}
