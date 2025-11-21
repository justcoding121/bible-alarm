using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Serilog;
using UIKit;

namespace Bible.Alarm.Platforms.iOS.Services.Handlers
{
    public class iOSAlarmHandler(
        ILogger logger,
        IPlaybackService playbackService,
        TaskScheduler taskScheduler)
        : IDisposable
    {
        private readonly ILogger _logger = logger;


        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1);

        //Need this to fix issue in XamarinMediaManager (notification stays on screen)
        private static bool firstTime = true;

        public async Task Handle(int scheduleId, bool isAlarm)
        {
            try
            {
                await Lock.WaitAsync();

                if (playbackService.IsPrepared)
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
                        await playbackService.PrepareAndPlay(scheduleId, isAlarm);
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "An error happened when ringing the alarm.");
                        throw;
                    }
                });
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error happened when creating the task to ring the alarm.");
                Dispose();
            }
            finally
            {
                Lock.Release();
            }
        }

        public void Dispose()
        {
            Dispose(false);
        }

        private bool _disposed;

        private void Dispose(bool disposeMediaManager)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposeMediaManager)
                {
                    Task.Delay(0)
                        .ContinueWith(_ => { UIApplication.SharedApplication.EndReceivingRemoteControlEvents(); },
                            taskScheduler);
                }
            }
        }
    }
}