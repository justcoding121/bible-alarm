using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using MediaManager;
using MediaManager.Player;
using Microsoft.EntityFrameworkCore;
using NLog;
using System;
using System.Threading;
using System.Threading.Tasks;
using UIKit;

namespace Bible.Alarm.iOS.Services.Handlers
{
    public class iOSAlarmHandler : IDisposable
    {
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;


        private IPlaybackService playbackService;
        private IMediaManager mediaManager;
        private TaskScheduler taskScheduler;

        private static SemaphoreSlim @lock = new SemaphoreSlim(1);

        //Need this to fix issue in XamarinMediaManager (notification stays on screen)
        private static bool firstTime = true;

        public iOSAlarmHandler(IPlaybackService playbackService,
                                IMediaManager mediaManager,
                                TaskScheduler taskScheduler)
        {
            this.playbackService = playbackService;
            this.mediaManager = mediaManager;
            this.taskScheduler = taskScheduler;

        }

        public async Task Handle(long scheduleId, bool isImmediate)
        {
            try
            {
                await @lock.WaitAsync();

                if (mediaManager.IsPreparedEx())
                {
                    Dispose();
                    return;
                }
                else
                {
                    await Task.Delay(0).ContinueWith((x) =>
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
                        await playbackService.PrepareAndPlay(scheduleId, isImmediate);
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, "An error happened when ringing the alarm.");
                        throw;
                    }
                });

            }
            catch (Exception e)
            {
                logger.Error(e, "An error happened when creating the task to ring the alarm.");
                Dispose();
            }
            finally
            {
                @lock.Release();
            }
        }

        public void Dispose()
        {
            dispose(false);
        }

        private bool disposed = false;
        private void dispose(bool disposeMediaManager)
        {
            if (!disposed)
            {
                disposed = true;
         
                if (disposeMediaManager)
                {
                    Task.Delay(0).ContinueWith((x) =>
                    {
                        UIApplication.SharedApplication.EndReceivingRemoteControlEvents();

                    }, taskScheduler);
                }
            }
        }
    }
}