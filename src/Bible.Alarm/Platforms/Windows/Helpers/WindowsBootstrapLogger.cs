using System.IO;
using Windows.Storage;

namespace Bible.Alarm.Platforms.Windows.Helpers;

/// <summary>
/// Writes unhandled exception details to LocalCache\logs\bootstrap.txt when Serilog may not be available.
/// Used only from App.UnhandledExceptionHandler.
/// </summary>
internal static class WindowsBootstrapLogger
{
    private const string LogFileName = "bootstrap.txt";

    public static void WriteException(Exception ex)
    {
        try
        {
            var basePath = ApplicationData.Current.LocalCacheFolder.Path;
            var logDir = Path.Combine(basePath, "logs");
            Directory.CreateDirectory(logDir);
            var path = Path.Combine(logDir, LogFileName);
            var line = $"{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z Exception: {ex}{Environment.NewLine}";
            File.AppendAllText(path, line);
            if (ex.InnerException != null)
            {
                var inner = $"{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z Inner: {ex.InnerException}{Environment.NewLine}";
                File.AppendAllText(path, inner);
            }
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
            var basePath = ApplicationData.Current.LocalCacheFolder.Path;
            var logDir = Path.Combine(basePath, "logs");
            Directory.CreateDirectory(logDir);
            var path = Path.Combine(logDir, LogFileName);
            var line = $"{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z {message}{Environment.NewLine}";
            File.AppendAllText(path, line);
        }
        catch
        {
            // Must not throw
        }
    }
}
