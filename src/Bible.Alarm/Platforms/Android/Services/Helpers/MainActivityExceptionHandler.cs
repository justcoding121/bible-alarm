#nullable enable

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

    private static void UnobservedTaskExceptionHandler(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AndroidBootstrapLogger.WriteException(e.Exception);
        logger.Error(e.Exception, "Unobserved task exception.");
        Log.CloseAndFlush();
    }

    private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        if (exception != null)
        {
            AndroidBootstrapLogger.WriteException(exception);
            logger.Error(exception, "Unhandled exception. IsTerminating: {IsTerminating}", e.IsTerminating);
        }
        else
        {
            AndroidBootstrapLogger.WriteLine($"Unhandled non-Exception: {e.ExceptionObject}. IsTerminating: {e.IsTerminating}");
            logger.Error("Unhandled exception (non-Exception object): {ExceptionObject}. IsTerminating: {IsTerminating}",
                e.ExceptionObject, e.IsTerminating);
        }

        Log.CloseAndFlush();
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
