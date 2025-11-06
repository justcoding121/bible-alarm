using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Common.Infrastructure;

public class SerilogSetup
{
    private static bool initialized = false;
    private static readonly object Lock = new();

    public static void Initialize(IVersionFinder versionFinder,
        string[] tags, string device, bool isLoggingEnabled = true)
    {
        CurrentDevice.RuntimePlatform = device;

        if (isLoggingEnabled)
            lock (Lock)
            {
                if (!initialized)
                {
                    SetupSerilog(versionFinder, tags);
                    initialized = true;
                }
            }
    }

    private static void SetupSerilog(IVersionFinder versionFinder, string[] tags)
    {
        var versionName = GetVersionName(versionFinder);

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithProperty("Application", AppConstants.AppSettings.ApplicationName)
            .Enrich.WithProperty("Version", versionName)
            .Enrich.WithProperty("Platform", CurrentDevice.RuntimePlatform);

#if DEBUG
        loggerConfig.Enrich.WithProperty("Environment", AppConstants.Logging.DebugEnvironment);
#endif

        // Add custom tags if provided
        if (tags != null)
            foreach (var tag in tags)
                loggerConfig.Enrich.WithProperty("Tag", tag);

        // Configure debug sink for Visual Studio debug window
#if DEBUG
        loggerConfig.WriteTo.Debug(
            outputTemplate: AppConstants.Logging.ConsoleOutputTemplate);
#endif

        // Configure console sink
        loggerConfig.WriteTo.Console(
            outputTemplate: AppConstants.Logging.ConsoleOutputTemplate);

        // Configure file sink for persistent logging
        loggerConfig.WriteTo.File(
            Path.Combine(FileSystem.Current.CacheDirectory, AppConstants.FilePaths.LogsDirectoryName, AppConstants.FilePaths.LogFileNamePattern),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: AppConstants.CacheSettings.LogFileRetentionDays,
            outputTemplate: AppConstants.Logging.FileOutputTemplate);

        Log.Logger = loggerConfig.CreateLogger();
    }

    private static string GetVersionName(IVersionFinder versionFinder)
    {
        try
        {
            return versionFinder.GetVersionName();
        }
        catch
        {
            return "AssemblyVersionNotFound";
        }
    }
}