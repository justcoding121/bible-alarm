using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using MediaManager;
using MediaManager.Platforms.Uap.Player;
using MediaManager.Player;
using Microsoft.EntityFrameworkCore;
using NLog;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media;

namespace Bible.Alarm.Services.Windows.Handlers
{
    public class UwpAlarmHandler : IDisposable
    {
        private static readonly Lazy<Logger> LazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger Logger => LazyLogger.Value;


        private IPlaybackService _playbackService;
        private IMediaManager _mediaManager;

        private static SemaphoreSlim @lock = new SemaphoreSlim(1);
        public UwpAlarmHandler(IPlaybackService playbackService,
                                IMediaManager mediaManager)
        {
            _playbackService = playbackService;
            _mediaManager = mediaManager;
   
            var windowsMediaPlayer = mediaManager.MediaPlayer as WindowsMediaPlayer;
            var mediaPlayer = windowsMediaPlayer.Player;

            mediaPlayer.SystemMediaTransportControls.IsEnabled = false;
        }

        public async Task Handle(long scheduleId, bool isImmediate)
        {
            try
            {
                await @lock.WaitAsync();

                if (_mediaManager.IsPreparedEx())
                {
                    Dispose();
                    return;
                }

                await Task.Run(async () =>
                {
                    try
                    {
                        await _playbackService.PrepareAndPlay(scheduleId, isImmediate);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "An error happened when ringing the alarm.");
                        throw;
                    }
                });

            }
            catch (Exception e)
            {
                Logger.Error(e, "An error happened when creating the task to ring the alarm.");
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