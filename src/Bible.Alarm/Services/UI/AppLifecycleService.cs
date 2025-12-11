#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.UI;

public class AppLifecycleService(ILogger logger, IServiceProvider serviceProvider) : IAppLifecycleService
{
    private readonly ILogger _logger = logger;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private bool _isDisposed;

    public void OnStart()
    {
        App.IsInForeground = true;

        // NOTE: Do NOT call InitializePlatformBootstrap here
        // WindowSetupService.CreateWindow() already handled bootstrap and sent InitializedMessage
        // This method is called AFTER CreateWindow(), so bootstrap is already complete

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000);

                var mediaIndexService = _serviceProvider.GetRequiredService<IMediaIndexService>();
                await mediaIndexService.UpdateIndexIfAvailable();

#if WINDOWS
                // Reschedule any enabled alarms that may have fired while app was closed
                // This is a fallback for WinUI 3 which doesn't have background tasks
                var schedulerService = _serviceProvider.GetRequiredService<ISchedulerService>();
                await schedulerService.HandleAsync();
#endif
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error happened inside OnStart task.");
            }
        });
    }

    public static void OnSleep()
    {
        App.IsInForeground = false;
    }

    public void OnResume()
    {
        App.IsInForeground = true;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000);

                var mediaIndexService = _serviceProvider.GetRequiredService<IMediaIndexService>();
                await mediaIndexService.UpdateIndexIfAvailable();

#if WINDOWS
                // Reschedule any enabled alarms that may have fired while app was in background
                // This is a fallback for WinUI 3 which doesn't have background tasks
                var schedulerService = _serviceProvider.GetRequiredService<ISchedulerService>();
                await schedulerService.HandleAsync();
#endif
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error happened inside OnResume task.");
            }
        });
    }
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        // No event handlers to unsubscribe, but dispose any non-singleton injected services if needed
        // All injected services are singletons, so no disposal needed
    }
}

