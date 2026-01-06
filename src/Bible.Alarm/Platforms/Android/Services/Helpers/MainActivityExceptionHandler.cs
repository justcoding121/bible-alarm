#nullable enable

using System;
using System.Threading.Tasks;
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

    private static void UnobservedTaskExceptionHandler(object? sender, UnobservedTaskExceptionEventArgs e) 
        => logger.Error(e.Exception, "Unobserved task exception.");

    private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        logger.Error(e.ExceptionObject as Exception, "Unhandled exception. IsTerminating: {IsTerminating}",
            e.IsTerminating);
    }
}
