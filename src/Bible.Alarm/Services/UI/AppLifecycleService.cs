#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Serilog;
#if WINDOWS
using Bible.Alarm.Platforms.Windows.Helpers;
#endif

namespace Bible.Alarm.Services.UI;

public sealed class AppLifecycleService(ILogger logger, IServiceProvider serviceProvider) : IAppLifecycleService, IDisposable
{
    private readonly IServiceProvider serviceProvider = serviceProvider;

#if WINDOWS
    private WindowsPeriodicBackgroundTasks? periodicBackgroundTasks;
#endif

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

#if WINDOWS
                // Start periodic background tasks (similar to Android JobScheduler)
                // This will run scheduler and media index update immediately on start, then continue periodically
                periodicBackgroundTasks ??= new WindowsPeriodicBackgroundTasks(logger, serviceProvider);
                periodicBackgroundTasks.Start();
#endif
            }
            catch (Exception e)
            {
                logger.Error(e, "An error happened inside OnStart task.");
            }
        });
    }

    public static void OnSleep() => App.IsInForeground = false;

    public void OnResume()
    {
        App.IsInForeground = true;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000);

#if WINDOWS
                // Ensure periodic background tasks are running
                // This will run scheduler and media index update immediately on resume, then continue periodically
                periodicBackgroundTasks ??= new WindowsPeriodicBackgroundTasks(logger, serviceProvider);
                periodicBackgroundTasks.Start();
#endif
            }
            catch (Exception e)
            {
                logger.Error(e, "An error happened inside OnResume task.");
            }
        });
    }

    public void Dispose()
    {
#if WINDOWS
        periodicBackgroundTasks?.Dispose();
        periodicBackgroundTasks = null;
#endif
    }
}

