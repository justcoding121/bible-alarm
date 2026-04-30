#nullable enable

using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Helpers;

/// <summary>
/// Handles global exception handling for MainActivity.
/// </summary>
public static class MainActivityExceptionHandler
{
    private static readonly ILogger logger = Log.ForContext(typeof(MainActivityExceptionHandler));

    /// <summary>
    /// Sets up global exception handlers for unhandled exceptions and unobserved task exceptions.
    /// </summary>
    public static void SetupGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;
    }

    private const int CrashFlushDelayMs = 500;

    private static void UnobservedTaskExceptionHandler(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AndroidBootstrapLogger.WriteException(e.Exception);
        logger.Error(e.Exception, AppConstants.Logging.ProcessDiagnosticsLog.UnobservedTaskException);
        FlushAndDelay();
    }

    private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        if (exception != null)
        {
            AndroidBootstrapLogger.WriteException(exception);
            logger.Error(exception, AppConstants.Logging.ProcessDiagnosticsLog.UnhandledExceptionIsTerminating, e.IsTerminating);
        }
        else
        {
            AndroidBootstrapLogger.WriteLine($"Unhandled non-Exception: {e.ExceptionObject}. IsTerminating: {e.IsTerminating}");
            logger.Error(AppConstants.Logging.ProcessDiagnosticsLog.UnhandledNonExceptionObjectIsTerminating,
                e.ExceptionObject, e.IsTerminating);
        }

        FlushAndDelay();
    }

    private static void FlushAndDelay()
    {
        try
        {
            Log.CloseAndFlush();
        }
        catch
        {
            // Best-effort Serilog flush during crash path; suppress so delay still runs for log drain.
        }

        Thread.Sleep(CrashFlushDelayMs);
    }

    /// <summary>
    /// Handles exceptions that occur during OnCreate.
    /// </summary>
    public static void HandleOnCreateException(Exception ex)
    {
        logger.Fatal(ex, "FATAL ERROR in OnCreate: {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);

        if (ex.InnerException != null)
        {
            logger.Fatal(ex.InnerException, "Inner exception in OnCreate: {ExceptionType}: {Message}",
                ex.InnerException.GetType().Name, ex.InnerException.Message);
        }

        try
        {
            logger.Fatal(ex, "Fatal error in MainActivity.OnCreate - app will crash");
        }
        catch
        {
            // Serilog not available, already logged with AndroidLog
        }
    }
}
