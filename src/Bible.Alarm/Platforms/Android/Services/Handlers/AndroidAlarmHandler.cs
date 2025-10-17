using System;
using System.Threading.Tasks;

using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Contracts.Media;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.Services.Droid;
using Com.Google.Android.Exoplayer2.UI;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Bible.Alarm.Droid.Services.Handlers
{
    public class AndroidAlarmHandler(
        IPlaybackService playbackService,
        ScheduleDbContext dbContext,
        DroidNotificationService notificationService)
        : IAndroidAlarmHandler, IDisposable
    {
        private static readonly Lazy<Logger> LazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger Logger => LazyLogger.Value;


        private bool _playbackServiceInitialized = false;
        private PlayerNotificationManager _playerNotificationManager;

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

            // MediaManager removed - using MediaElement instead

            await Task.Run(async () =>
            {
                try
                {
                    await playbackService.PrepareAndPlay(scheduleId, isImmediate);

                    _playbackServiceInitialized = true;

                    // Notification manager removed - using MediaElement instead
                }
                catch (Exception e)
                {
                    Logger.Error(e, "An error happened when ringing the alarm.");
                    Dispose();
                }
            });
        }

        private async void NotificationCancelled(object sender, PlayerNotificationManager.NotificationCancelledEventArgs e)
        {
            if (e.DismissedByUser)
            {
                await playbackService.Dismiss();
            }
        }

        private bool _disposed = false;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_playerNotificationManager != null)
            {
                _playerNotificationManager.NotificationCancelled -= NotificationCancelled;
            }

            dbContext.Dispose();
            notificationService.Dispose();

            if (_playbackServiceInitialized)
            {
                // MediaManager removed - using MediaElement instead
            }

            Disposed?.Invoke(this, true);
        }
    }
}