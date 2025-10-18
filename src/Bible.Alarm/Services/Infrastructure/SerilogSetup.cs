using Bible.Alarm.Contracts.Platform;
using Serilog;

namespace Bible.Alarm.Services.Infrastructure;

public class SerilogSetup
{
    private static bool initialized = false;
    private static object @lock = new();

    public static void Initialize(IVersionFinder versionFinder,
        string[] tags, string device, bool isLoggingEnabled = true)
    {
        CurrentDevice.RuntimePlatform = device;

        if (isLoggingEnabled)
            lock (@lock)
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
            .Enrich.WithProperty("Application", "Bible-Alarm")
            .Enrich.WithProperty("Version", versionName)
            .Enrich.WithProperty("Platform", CurrentDevice.RuntimePlatform);

#if DEBUG
        loggerConfig.Enrich.WithProperty("Environment", "DEBUG");
#endif

        // Add custom tags if provided
        if (tags != null)
            foreach (var tag in tags)
                loggerConfig.Enrich.WithProperty("Tag", tag);

        // Configure console sink
        loggerConfig.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

        // Configure file sink for persistent logging
        loggerConfig.WriteTo.File(
            Path.Combine(FileSystem.Current.CacheDirectory, "logs", "bible-alarm-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate:
            "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

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