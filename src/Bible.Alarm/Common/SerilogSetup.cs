using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Common;

public class SerilogSetup
{
    private static bool initialized;
    private static readonly Lock @lock = new();

    public static void Initialize(IVersionFinder versionFinder,
        string[] tags, string device, bool isLoggingEnabled = true)
    {
        CurrentDevice.RuntimePlatform = device;

        if (isLoggingEnabled)
        {
            lock (@lock)
            {
                if (!initialized)
                {
                    SetupSerilog(versionFinder, tags);
                    initialized = true;
                }
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
        {
            foreach (var tag in tags)
            {
                loggerConfig.Enrich.WithProperty("Tag", tag);
            }
        }

        // Configure debug sink for Visual Studio Output window
        // Serilog's Debug sink writes to the Visual Studio Output window
        // Works on Windows, Android, and iOS when debugging in Visual Studio
        // Make sure to select "Debug" in the Output window's "Show output from:" dropdown
#if DEBUG
        loggerConfig.WriteTo.Debug(
            outputTemplate: AppConstants.Logging.ConsoleOutputTemplate);
#endif

        Log.Logger = loggerConfig.CreateLogger();
    }

    private static string GetVersionName(IVersionFinder versionFinder)
    {
        try
        {
            return versionFinder.GetVersionName();
        }
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Failed to get version name from version finder, using fallback");
            return "AssemblyVersionNotFound";
        }
    }
}
