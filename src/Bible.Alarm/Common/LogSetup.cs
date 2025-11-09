using Bible.Alarm.Common.Interfaces.Platform;
using Serilog;

namespace Bible.Alarm.Common;

public class LogSetup
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

        // Configure debug sink for Visual Studio debug window
#if DEBUG
        loggerConfig.WriteTo.Debug(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
#endif

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

public static class JsonConvertExtension
{
    public static string SerializeObject(this object @object)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Serialize(@object);
        }
        catch
        {
            return null;
        }
    }
}