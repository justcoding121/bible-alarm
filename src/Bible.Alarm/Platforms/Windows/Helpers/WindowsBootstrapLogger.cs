#nullable enable

using System;
using System.IO;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
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
                var line = BootstrapLogLineFormatter.FormatUtcDiagnosticLine(DateTime.UtcNow, $"Exception: {text}");
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
                var line = BootstrapLogLineFormatter.FormatUtcDiagnosticLine(DateTime.UtcNow, message);
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
        if (!BootstrapLogRootResolver.TryGetLogRoot(
                out var root,
                () => ApplicationData.Current.LocalCacheFolder.Path,
                () => AppContext.BaseDirectory ?? Directory.GetCurrentDirectory()))
        {
            return false;
        }

        try
        {
            var logsDir = AppConstants.FilePaths.LogsDirectoryName;
            var fileName = AppConstants.FilePaths.BootstrapDiagnosticLogFileName;
            BootstrapLogPathArguments.Validate(root, logsDir, fileName);
            logFilePath = BootstrapDiagnosticLogPathComposer.ComposeLogFilePath(root, logsDir, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
