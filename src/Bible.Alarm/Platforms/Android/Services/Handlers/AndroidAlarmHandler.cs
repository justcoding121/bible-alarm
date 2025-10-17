using System;
using System.Threading.Tasks;

using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Contracts.Media;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.Services.Droid;
using Com.Google.Android.Exoplayer2.UI;
using MediaManager;
using MediaManager.Platforms.Android.Notifications;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Bible.Alarm.Droid.Services.Handlers
{
    public class AndroidAlarmHandler : IAndroidAlarmHandler, IDisposable
    {
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;


        private IMediaManager mediaManager;
        private IPlaybackService playbackService;
        private bool mediaManagerInitialized = false;
        private PlayerNotificationManager playerNotificationManager;
        private DroidNotificationService notificationService;
        private ScheduleDbContext dbContext;

        public AndroidAlarmHandler(IMediaManager mediaManager,
            IPlaybackService playbackService,
            ScheduleDbContext dbContext,
            DroidNotificationService notificationService)
        {
            this.mediaManager = mediaManager;
            this.playbackService = playbackService;
            this.notificationService = notificationService;
            this.dbContext = dbContext;

        }

        public event EventHandler<bool> Disposed;

        public async Task Handle(long scheduleId, bool isImmediate)
        {
            var schedule = await dbContext.AlarmSchedules.FirstOrDefaultAsync(x => x.Id == scheduleId);

            if (schedule == null)
            {
                Dispose();
                return;
            }

            //local notification for android
            if (!isImmediate)
            {
                if (schedule.NotificationEnabled)
                {
                    notificationService.RemoveLocalNotification(schedule.Id);
                    notificationService.ShowLocalNotification(schedule.Id, string.IsNullOrEmpty(schedule.Name) ? "Bible Alarm" : schedule.Name,
                                                                     "Press to start listening now.");
                    Dispose();
                    return;
                }
            }

            if (schedule.NotificationEnabled)
            {
                notificationService.RemoveLocalNotification(schedule.Id);
            }

            mediaManager.Init(Android.App.Application.Context);

            await Task.Run(async () =>
            {
                try
                {
                    await playbackService.PrepareAndPlay(scheduleId, isImmediate);

                    mediaManagerInitialized = true;

                    playerNotificationManager = (mediaManager.Notification as NotificationManager).PlayerNotificationManager;
                    playerNotificationManager.NotificationCancelled += notificationCancelled;
                }
                catch (Exception e)
                {
                    logger.Error(e, "An error happened when ringing the alarm.");
                    Dispose();
                }
            });
        }

        private async void notificationCancelled(object sender, PlayerNotificationManager.NotificationCancelledEventArgs e)
        {
            if (e.DismissedByUser)
            {
                await mediaManager.Stop();
            }
        }

        private bool disposed = false;
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;

                if (playerNotificationManager != null)
                {
                    playerNotificationManager.NotificationCancelled -= notificationCancelled;
                }

                dbContext.Dispose();
                notificationService.Dispose();

                if (mediaManagerInitialized)
                {
                    mediaManager?.Dispose();
                }

                Disposed?.Invoke(this, true);
            }
        }
    }
}