#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Services.UI;

public sealed class ExceptionHandlingService(ILogger logger) : IExceptionHandlingService
{
    private const int CrashFlushDelayMs = 500;
    private bool isDisposed;

    public void SetupGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;
    }

    private void UnobservedTaskExceptionHandler(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        logger.Error(e.Exception, AppConstants.Logging.ProcessDiagnosticsLog.UnobservedTaskException);
        FlushAndDelay();
    }

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        logger.Error(e.ExceptionObject as Exception, AppConstants.Logging.ProcessDiagnosticsLog.UnhandledExceptionIsTerminating,
            e.IsTerminating);
        FlushAndDelay();
    }

    /// <summary>
    /// Flushes the Serilog async sink and waits briefly to let the OS complete the file write
    /// before the process terminates. Without this delay the async sink buffer may be lost.
    /// </summary>
    private static void FlushAndDelay()
    {
        try
        {
            Log.CloseAndFlush();
        }
        catch
        {
        }

        Thread.Sleep(CrashFlushDelayMs);
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Unsubscribe from global exception handlers
        AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException -= UnobservedTaskExceptionHandler;
    }
}

