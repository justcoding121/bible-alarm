using System.IO;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Platforms.Android.Services.Helpers;

/// <summary>
/// Writes unhandled/unobserved exception details to app cache/logs/bootstrap.txt when Serilog may not have flushed.
/// Same pattern as Windows/iOS so crashes leave a trace (retrievable via adb or file access).
/// </summary>
internal static class AndroidBootstrapLogger
{
    public static void WriteException(Exception ex)
    {
        try
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
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
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
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
