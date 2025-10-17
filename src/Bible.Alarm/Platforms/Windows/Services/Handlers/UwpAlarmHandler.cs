using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using MediaManager;
using MediaManager.Platforms.Uap.Player;
using MediaManager.Player;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Fluent;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media;

namespace Bible.Alarm.UWP.Services.Handlers
{
    public class UwpAlarmHandler : IDisposable
    {
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;


        private IPlaybackService playbackService;
        private IMediaManager mediaManager;

        private static SemaphoreSlim @lock = new SemaphoreSlim(1);
        public UwpAlarmHandler(IPlaybackService playbackService,
                                IMediaManager mediaManager)
        {
            this.playbackService = playbackService;
            this.mediaManager = mediaManager;
   
            var windowsMediaPlayer = mediaManager.MediaPlayer as WindowsMediaPlayer;
            var mediaPlayer = windowsMediaPlayer.Player;

            mediaPlayer.SystemMediaTransportControls.IsEnabled = false;
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
            
        }

    }
}