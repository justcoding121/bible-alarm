using System.IO;
using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Platforms.Android.Services.Helpers;

/// <summary>
/// Writes unhandled/unobserved exception details to app cache/logs/bootstrap.txt when Serilog may not have flushed.
/// Same pattern as Windows/iOS so crashes leave a trace (retrievable via adb or file access).
/// </summary>
internal static class AndroidBootstrapLogger
{
    private const string LogFileName = "bootstrap.txt";

    public static void WriteException(Exception ex)
    {
        try
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logDir = Path.Combine(basePath, AppConstants.FilePaths.LogsDirectoryName);
            Directory.CreateDirectory(logDir);
            var path = Path.Combine(logDir, LogFileName);
            var text = ex.ToString();
            var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z Exception: {text}{Environment.NewLine}";
            File.AppendAllText(path, line);
        }
        catch
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
            var path = Path.Combine(logDir, LogFileName);
            var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z {message}{Environment.NewLine}";
            File.AppendAllText(path, line);
        }
        catch
        {
            // Must not throw
        }
    }
}
