#if IOS
using Bible.Alarm.Platforms.iOS.Logging;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Services.Logging;

/// <summary>
/// Service to access iOS log files for debugging
/// Provides methods to read log content
/// </summary>
public class IOSLogAccessService
{
    private static IOSLogFileSink? logSink;
    private static readonly ILogger logger = Log.ForContext<IOSLogAccessService>();

    public static void RegisterSink(IOSLogFileSink sink)
    {
        logSink = sink;
        logger.Information("IOSLogAccessService: Log sink registered - Log file: {LogPath}", sink.LogFilePath);
    }

    /// <summary>
    /// Gets recent log content (last 1000 lines)
    /// </summary>
    public static string GetRecentLogs()
    {
        if (logSink == null)
        {
            return "Log sink not registered";
        }

        try
        {
            return logSink.GetRecentLogs();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to get recent logs");
            return $"Error reading logs: {ex.Message}";
        }
    }

    /// <summary>
    /// Gets the log file path
    /// </summary>
    public static string GetLogFilePath()
    {
        if (logSink == null)
        {
            return "Log sink not registered";
        }

        return logSink.LogFilePath;
    }

    /// <summary>
    /// Reads the entire log file content
    /// </summary>
    public static string ReadFullLogFile()
    {
        if (logSink == null)
        {
            return "Log sink not registered";
        }

        try
        {
            var logPath = logSink.LogFilePath;
            if (File.Exists(logPath))
            {
                return File.ReadAllText(logPath);
            }

            return "Log file does not exist";
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to read full log file");
            return $"Error reading log file: {ex.Message}";
        }
    }
}
#endif
