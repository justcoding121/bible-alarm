using System.Text.Json;
using Bible.Alarm.Common.Interfaces.Platform;
using Serilog;

namespace Bible.Alarm.Common;

public class LogSetup
{
    private static bool initialized;
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

        // Configure debug sink for Visual Studio Output window
        // Serilog's Debug sink writes to the Visual Studio Output window
        // Works on Windows, Android, and iOS when debugging in Visual Studio
        // Make sure to select "Debug" in the Output window's "Show output from:" dropdown
#if DEBUG
        loggerConfig.WriteTo.Debug(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
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

public static class JsonConvertExtension
{
    public static string SerializeObject(this object @object)
    {
        try
        {
            // Configure JSON serializer to ignore non-serializable properties like MethodBase (TargetSite)
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                // Exclude properties that can't be serialized (like TargetSite.MethodBase)
                IgnoreReadOnlyProperties = false
            };
            
            // Handle UnhandledExceptionEventArgs - extract and serialize only the exception
            if (@object is System.UnhandledExceptionEventArgs unhandledArgs)
            {
                var unhandledException = unhandledArgs.ExceptionObject as Exception;
                if (unhandledException != null)
                {
                    return SerializeException(unhandledException, options);
                }
                else
                {
                    // Non-Exception object - just serialize basic info
                    return JsonSerializer.Serialize(new
                    {
                        ExceptionObject = unhandledArgs.ExceptionObject?.ToString(),
                        IsTerminating = unhandledArgs.IsTerminating
                    }, options);
                }
            }
            
            // For Exception objects, serialize only the essential properties
            if (@object is Exception exception)
            {
                return SerializeException(exception, options);
            }
            
            return JsonSerializer.Serialize(@object, options);
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Failed to serialize object to JSON");
            return null;
        }
    }
    
    private static string SerializeException(Exception exception, JsonSerializerOptions options)
    {
        var exceptionInfo = new
        {
            Type = exception.GetType().FullName,
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            Source = exception.Source,
            HResult = exception.HResult,
            HelpLink = exception.HelpLink,
            InnerException = exception.InnerException != null ? new
            {
                Type = exception.InnerException.GetType().FullName,
                Message = exception.InnerException.Message,
                StackTrace = exception.InnerException.StackTrace,
                Source = exception.InnerException.Source
            } : null
        };
        return JsonSerializer.Serialize(exceptionInfo, options);
    }
}