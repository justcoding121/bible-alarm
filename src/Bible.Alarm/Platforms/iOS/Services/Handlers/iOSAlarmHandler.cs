using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Platforms.iOS.Services.Handlers.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using UIKit;

namespace Bible.Alarm.Platforms.iOS.Services.Handlers;

public sealed class IOsAlarmHandler(
    ILogger logger,
    IPlaybackService playbackService,
    IState<PlaybackState> playbackState,
    TaskScheduler taskScheduler)
    : IIosAlarmHandler
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

                await Task.Delay(0).ContinueWith(_ =>
                {
                    if (!firstTime)
                    {
                        UIApplication.SharedApplication.BeginReceivingRemoteControlEvents();
                    }

                    firstTime = false;
                }, taskScheduler);


                WeakReferenceMessenger.Default.Send(new RequestShowPlaybackModalMessage { TargetScheduleId = scheduleId });
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

        // @lock is static — shared across all handler instances for the lifetime of the process.
        // Disposing it here would permanently break any future handler instance that tries to
        // acquire it (WaitAsync on a disposed SemaphoreSlim throws ObjectDisposedException).
        // The OS reclaims unmanaged resources on process exit, so no disposal is needed.

        // All injected services (playbackService, IState<PlaybackState>, TaskScheduler) are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}
