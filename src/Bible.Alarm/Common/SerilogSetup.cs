using System.Runtime.InteropServices;
using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Shared.Constants;
using Serilog;
#if ANDROID
using Bible.Alarm.Platforms.Android.Logging;
#endif

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
        else
        {
            // If logging is disabled, use null logger (no output)
            lock (@lock)
            {
                if (!initialized)
                {
                    Log.Logger = new LoggerConfiguration()
                        .MinimumLevel.Fatal() // Only fatal errors, but no sinks so nothing is logged
                        .CreateLogger();
                    initialized = true;
                }
            }
        }
    }

    private static void SetupSerilog(IVersionFinder versionFinder, string[] tags)
    {
        var versionName = GetVersionName(versionFinder);

        var loggerConfig = new LoggerConfiguration()
            .Enrich.WithProperty("Application", AppConstants.AppSettings.ApplicationName)
            .Enrich.WithProperty("Version", versionName)
            .Enrich.WithProperty("Platform", CurrentDevice.RuntimePlatform);

#if DEBUG
        // DEBUG mode: Write to Debug sink and file with all levels
        loggerConfig.MinimumLevel.Debug(); // All levels in DEBUG mode
        loggerConfig.Enrich.WithProperty("Environment", AppConstants.Logging.DebugEnvironment);

        // Configure debug sink for Visual Studio Output window using Async wrapper
        // The Async wrapper ensures logging calls don't block the calling thread,
        // which is critical for UI responsiveness during debugging
        // Serilog's Debug sink writes to the Visual Studio Output window
        // Works on Windows, Android, and iOS when debugging in Visual Studio
        // Make sure to select "Debug" in the Output window's "Show output from:" dropdown
        loggerConfig.WriteTo.Async(a => a.Debug(
            outputTemplate: AppConstants.Logging.ConsoleOutputTemplate));

#if !ANDROID
        // Configure file logging (DEBUG mode, non-Android only). Android uses logcat / Debug sink only.
        var logDirectory = GetLogDirectory();
        if (!string.IsNullOrEmpty(logDirectory))
        {
            try
            {
                Directory.CreateDirectory(logDirectory);
                DeleteTodaysLogFile(logDirectory);
                var logFilePath = Path.Combine(logDirectory, AppConstants.FilePaths.LogFileNamePattern + ".txt");
                loggerConfig.WriteTo.Async(a => a.File(
                    path: logFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: AppConstants.Logging.ConsoleOutputTemplate,
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug,
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1)));
            }
            catch (Exception ex)
            {
                try
                {
                    var fallbackLogger = loggerConfig.CreateLogger();
                    fallbackLogger.Error(ex, "Failed to configure file logging");
                }
                catch
                {
                }
            }
        }
#endif
#else
        // RELEASE mode: Error and above only
        loggerConfig.MinimumLevel.Error();

#if ANDROID
        // Android release: logcat only (no file sink). Use: adb logcat -s BibleAlarm
        loggerConfig.WriteTo.Sink(
            new AndroidLogcatSink(),
            Serilog.Events.LogEventLevel.Error);
#else
        // Non-Android release: file logging only
        var logDirectory = GetLogDirectory();
        if (!string.IsNullOrEmpty(logDirectory))
        {
            try
            {
                Directory.CreateDirectory(logDirectory);
                var logFilePath = Path.Combine(logDirectory, AppConstants.FilePaths.LogFileNamePattern + ".txt");
                loggerConfig.WriteTo.Async(a => a.File(
                    path: logFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: AppConstants.Logging.ConsoleOutputTemplate,
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error,
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(2)));
            }
            catch (Exception ex)
            {
                _ = ex;
            }
        }
#endif
#endif

        // Add custom tags if provided
        if (tags != null)
        {
            foreach (var tag in tags)
            {
                loggerConfig.Enrich.WithProperty("Tag", tag);
            }
        }

        Log.Logger = loggerConfig.CreateLogger();
    }

    private static string GetLogDirectory()
    {
        try
        {
            // Use platform-specific cache directory that OS can clear when needed
            string cacheBasePath;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: Use cache subfolder in LocalApplicationData
                // This location can be cleared by disk cleanup tools
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                cacheBasePath = Path.Combine(localAppData, "Bible.Alarm", "Cache");
            }
            else if (CurrentDevice.RuntimePlatform == "Android")
            {
                // Android: LocalApplicationData maps to the app's cache directory
                // This is automatically cleared by the OS when storage is low or app is uninstalled
                cacheBasePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || CurrentDevice.RuntimePlatform == "iOS")
            {
                // iOS: Use Caches directory which OS can clear when storage is low
                // Path: ~/Library/Caches
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                cacheBasePath = Path.Combine(documentsPath, "..", "Library", "Caches");
            }
            else
            {
                // Fallback for other platforms
                cacheBasePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }

            return Path.Combine(cacheBasePath, AppConstants.FilePaths.LogsDirectoryName);
        }
        catch
        {
            // Fallback to current directory if platform-specific path fails
            return Path.Combine(Directory.GetCurrentDirectory(), AppConstants.FilePaths.LogsDirectoryName);
        }
    }

    private static string GetVersionName(IVersionFinder versionFinder)
    {
        try
        {
            return versionFinder.GetVersionName();
        }
        catch (Exception ex)
        {
#if DEBUG
            Log.Logger.Debug(ex, "Failed to get version name from version finder, using fallback");
#else
            _ = ex; // Suppress unused variable warning in Release builds
#endif
            return "AssemblyVersionNotFound";
        }
    }

#if DEBUG
    /// <summary>
    /// Deletes today's log file on app startup in DEBUG mode.
    /// This provides a clean slate for debugging each install.
    /// </summary>
    private static void DeleteTodaysLogFile(string logDirectory)
    {
        try
        {
            if (!Directory.Exists(logDirectory))
            {
                return;
            }

            // Get today's date in the format used by Serilog rolling file (YYYYMMDD)
            // Serilog creates files like "bible-alarm-20260106.txt" with RollingInterval.Day
            var today = DateTime.Now.ToString("yyyyMMdd");
            var logFileName = $"{AppConstants.FilePaths.LogFileNamePattern}{today}.txt";
            var logFilePath = Path.Combine(logDirectory, logFileName);

            if (File.Exists(logFilePath))
            {
                File.Delete(logFilePath);
                // Use Serilog if available, otherwise silently continue (not critical)
                try
                {
                    Log.Logger?.Debug("Deleted today's log file: {LogFilePath}", logFilePath);
                }
                catch
                {
                    // Serilog not initialized yet or failed - silently continue
                }
            }
        }
        catch (Exception ex)
        {
            // Don't throw - log file deletion is not critical
            // Use Serilog if available, otherwise silently continue
            try
            {
                Log.Logger?.Warning(ex, "Failed to delete today's log file");
            }
            catch
            {
                // Serilog not initialized yet or failed - silently continue
            }
        }
    }
#endif
}
