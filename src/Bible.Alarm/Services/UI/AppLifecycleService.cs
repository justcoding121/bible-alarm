#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.UI;

public class AppLifecycleService(ILogger logger, IServiceProvider serviceProvider)
{
    private readonly ILogger _logger = logger;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public void OnStart()
    {
        App.IsInForeground = true;

        MauiProgram.InitializePlatformBootstrap(_serviceProvider, isForeground: true);

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000);

                var mediaIndexService = _serviceProvider.GetRequiredService<MediaIndexService>();
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

                var mediaIndexService = _serviceProvider.GetRequiredService<MediaIndexService>();
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
}

