using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Platforms.iOS.Services.Handlers.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;
using UIKit;

namespace Bible.Alarm.Platforms.iOS.Services.Handlers;

public class IOsAlarmHandler(
    ILogger logger,
    IPlaybackService playbackService,
    IState<PlaybackState> playbackState,
    TaskScheduler taskScheduler)
    : IIOsAlarmHandler
{
    private static readonly SemaphoreSlim @lock = new(1);

    //Need this to fix issue in XamarinMediaManager (notification stays on screen)
    private static bool firstTime = true;

    public async Task HandleAsync(int scheduleId, bool isAlarm)
    {
        try
        {
            await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
            {
                if (playbackState.Value.IsPreparingOrPlaying)
                {
                    Dispose();
                    return;
                }
                else
                {
                    await Task.Delay(0).ContinueWith(_ =>
                    {
                        if (!firstTime)
                        {
                            UIApplication.SharedApplication.BeginReceivingRemoteControlEvents();
                        }

                        firstTime = false;
                    }, taskScheduler);
                }


                await Task.Run(async () =>
                {
                    try
                    {
                        await playbackService.PrepareAndPlayAsync(scheduleId, isAlarm);
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, "An error happened when ringing the alarm.");
                        throw;
                    }
                });
            });
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened when creating the task to ring the alarm.");
            Dispose();
        }
    }

    private bool isDisposed;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Dispose static semaphore
        try
        {
            @lock.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore if already disposed
            logger.Warning(ex, "Error disposing semaphore, may already be disposed");
        }

        // All injected services (playbackService, IState<PlaybackState>, TaskScheduler) are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}
