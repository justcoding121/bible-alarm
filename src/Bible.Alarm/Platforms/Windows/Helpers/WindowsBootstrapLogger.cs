using System.IO;
using Windows.Storage;

namespace Bible.Alarm.Platforms.Windows.Helpers;

/// <summary>
/// Writes unhandled exception details to logs\bootstrap.txt when Serilog may not be available.
/// Packaged: LocalCache\logs. Unpackaged (exe from bin\Release\...\win-x64): fallback to exe directory\logs.
/// </summary>
internal static class WindowsBootstrapLogger
{
    private const string LogFileName = "bootstrap.txt";
    private const string LogsDirName = "logs";

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
            catch
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
            catch
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
            var logDir = Path.Combine(basePath, LogsDirName);
            Directory.CreateDirectory(logDir);
            logFilePath = Path.Combine(logDir, LogFileName);
            return true;
        }
        catch
        {
            // Unpackaged (exe from bin\Release\...\win-x64): ApplicationData can fail; use exe directory\logs
            try
            {
                var exeDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
                var logDir = Path.Combine(exeDir, LogsDirName);
                Directory.CreateDirectory(logDir);
                logFilePath = Path.Combine(logDir, LogFileName);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
