using System.IO;
using Bible.Alarm.Shared.Constants;
using Windows.Storage;

namespace Bible.Alarm.Platforms.Windows.Helpers;

/// <summary>
/// Writes unhandled exception details to logs\bootstrap.txt when Serilog may not be available.
/// Packaged: LocalCache\logs. Unpackaged (exe from bin\Release\...\win-x64): fallback to exe directory\logs.
/// </summary>
internal static class WindowsBootstrapLogger
{
    public static void WriteException(Exception ex)
    {
        if (TryGetLogPath(out var path))
        {
            try
            {
                var text = ex.ToString();
                var line = $"{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z Exception: {text}{Environment.NewLine}";
                File.AppendAllText(path, line);
            }
            catch (Exception)
            {
                // Must not throw
            }
        }
    }

    public static void WriteLine(string message)
    {
        if (TryGetLogPath(out var path))
        {
            try
            {
                var line = $"{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z {message}{Environment.NewLine}";
                File.AppendAllText(path, line);
            }
            catch (Exception)
            {
                // Must not throw
            }
        }
    }

    private static bool TryGetLogPath(out string logFilePath)
    {
        logFilePath = null!;
        try
        {
            var basePath = ApplicationData.Current.LocalCacheFolder.Path;
            var logDir = Path.Combine(basePath, AppConstants.FilePaths.LogsDirectoryName);
            Directory.CreateDirectory(logDir);
            logFilePath = Path.Combine(logDir, AppConstants.FilePaths.BootstrapDiagnosticLogFileName);
            return true;
        }
        catch (Exception)
        {
            // Unpackaged (exe from bin\Release\...\win-x64): ApplicationData can fail; use exe directory\logs
            try
            {
                var exeDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
                var logDir = Path.Combine(exeDir, AppConstants.FilePaths.LogsDirectoryName);
                Directory.CreateDirectory(logDir);
                logFilePath = Path.Combine(logDir, AppConstants.FilePaths.BootstrapDiagnosticLogFileName);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
