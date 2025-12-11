#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Services.UI.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.UI;

public class ExceptionHandlingService(ILogger logger) : IExceptionHandlingService
{
    private readonly ILogger _logger = logger;
    private bool _isDisposed;

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
        _logger.Error(e.ExceptionObject as Exception, "Unhandled exception. IsTerminating: {IsTerminating}", 
            e.IsTerminating);
    }
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        // Unsubscribe from global exception handlers
        AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException -= UnobservedTaskExceptionHandler;
    }
}

