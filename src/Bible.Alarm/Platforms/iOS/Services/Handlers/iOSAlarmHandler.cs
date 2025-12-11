using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Platforms.iOS.Services.Handlers.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;
using UIKit;

namespace Bible.Alarm.Platforms.iOS.Services.Handlers
{
    public class iOSAlarmHandler(
        ILogger logger,
        IPlaybackService playbackService,
        IState<PlaybackState> playbackState,
        TaskScheduler taskScheduler)
        : IiOSAlarmHandler
    {
        private readonly ILogger _logger = logger;
        private readonly IState<PlaybackState> _playbackState = playbackState;


        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1);

        //Need this to fix issue in XamarinMediaManager (notification stays on screen)
        private static bool firstTime = true;

        public async Task HandleAsync(int scheduleId, bool isAlarm)
        {
            try
            {
                await ConcurrencyHelper.ExecuteAsync(Lock, async () =>
                {
                    if (_playbackState.Value.IsPreparingOrPlaying)
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
                            _logger.Error(e, "An error happened when ringing the alarm.");
                            throw;
                        }
                    });
                });
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error happened when creating the task to ring the alarm.");
                Dispose();
            }
        }

        private bool _isDisposed;
        
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            
            _isDisposed = true;
            
            // Dispose static semaphore
            try
            {
                Lock.Dispose();
            }
            catch (Exception ex)
            {
                // Ignore if already disposed
                _logger.Warning(ex, "Error disposing semaphore, may already be disposed");
            }
            
            // All injected services (playbackService, IState<PlaybackState>, TaskScheduler) are singletons
            // and should not be disposed here as they are managed by the DI container
        }
    }
}