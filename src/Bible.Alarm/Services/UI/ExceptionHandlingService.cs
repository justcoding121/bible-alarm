#nullable enable
using Bible.Alarm.Common;
using Serilog;

namespace Bible.Alarm.Services.UI;

public class ExceptionHandlingService(ILogger logger)
{
    private readonly ILogger _logger = logger;

    public void SetupGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;
    }

    private void UnobservedTaskExceptionHandler(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.Error(e.Exception, "Unobserved task exception.");
    }

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        _logger.Error("Unhandled exception.", e.SerializeObject());
    }
}

