using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V4.App;
using Bible.Alarm.Droid;
using Bible.Alarm.Droid.Services.Handlers;
using Bible.Alarm.Droid.Services.Tasks;
using Bible.Alarm.Models;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.Services.Droid.Tasks;
using Java.Lang;
using TaskStackBuilder = Android.Support.V4.App.TaskStackBuilder;
using NLog;
using System;
using System.Threading.Tasks;
using Android.Graphics.Drawables;
using Android.Support.V4.Content;
using Android.Graphics;
using Bible.Alarm.Contracts.Media;
using System.IO;
using Android.Media;
using Android.Content.Res;

namespace Bible.Alarm.Services.Droid
{
    public class DroidNotificationService : INotificationService
    {
        public static readonly string CHANNEL_ID_AND_NAME = "alarm_notification";
        public static readonly string CHANNEL_DESCRIPTION = "alarm_notification are send to this channel";
        public static readonly string SCHEDULE_ID = "schedule_id";

        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;


        private readonly IContainer container;
        private readonly IStorageService storageService;

        public DroidNotificationService(IContainer container, IStorageService storageService)
        {
            this.container = container;
            this.storageService = storageService;
        }

        public async Task ShowNotification(long scheduleId)
        {
            try
            {
                var context = container.AndroidContext();
                var alarmIntent = new Intent(context, typeof(AlarmRingerReceiver));
                alarmIntent.PutExtra("ScheduleId", scheduleId.ToString());
                alarmIntent.PutExtra("IsImmediate", true);

                context.SendBroadcast(alarmIntent);
            }
            catch (System.Exception e)
            {
                logger.Error(e, "Error happened when playing alarm manually.");
                await Task.Delay(1500);
                throw;
            }
        }

        public Task ScheduleNotification(AlarmSchedule schedule,
            string title, string body)
        {
            var time = schedule.NextFireDate();

            if (container.IsAndroidService())
            {
                AlarmSetupService.ScheduleNotification(container.AndroidContext(), schedule.Id, time, title, body);
            }
            else
            {
                Intent intent = new Intent(container.AndroidContext(), typeof(AlarmSetupService));
                intent.PutExtra("Action", "Add");
                intent.PutExtra("ScheduleId", schedule.Id.ToString());
                intent.PutExtra("Time", time.ToString());
                intent.PutExtra("Title", title);
                intent.PutExtra("Body", body);
                container.AndroidContext().StartService(intent);
            }

            return Task.CompletedTask;
        }

        public void ShowLocalNotification(int scheduleId, string title, string body)
        {
            var notificationManagerCompat = NotificationManagerCompat.From(Application.Context);

            // Pass the current button press count value to the next activity:
            var valuesForActivity = new Bundle();
            valuesForActivity.PutInt(SCHEDULE_ID, scheduleId);

            var resultIntent = new Intent(Application.Context, typeof(MainActivity));
            resultIntent.PutExtras(valuesForActivity);

            var stackBuilder = TaskStackBuilder.Create(Application.Context);
            stackBuilder.AddParentStack(Class.FromType(typeof(MainActivity)));
            stackBuilder.AddNextIntent(resultIntent);

            // Create the PendingIntent with the back stack:
            var resultPendingIntent = stackBuilder.GetPendingIntent(0, (int)(PendingIntentFlags.UpdateCurrent));

            var drawable = ContextCompat.GetDrawable(Application.Context, Resource.Drawable.ic_launcher_round);
            var bitmap = drawableToBitmap(drawable);

            // Build the notification:
            var builder = new NotificationCompat.Builder(Application.Context, CHANNEL_ID_AND_NAME)
                          .SetAutoCancel(true)
                          .SetContentIntent(resultPendingIntent)
                          .SetContentTitle(title)
                          .SetSmallIcon(Resource.Drawable.exo_icon_circular_play)
                          .SetLargeIcon(bitmap)
                          .SetContentText(body);

            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            {

                var soundUri = Android.Net.Uri.Parse("android.resource://" + Application.Context.PackageName + "/" + Resource.Raw.cool_alarm_tone_notification_sound);

                builder.SetSound(soundUri);
                builder.SetDefaults(0);
            }

            notificationManagerCompat.Notify(scheduleId, builder.Build());
        }

        private static Bitmap drawableToBitmap(Drawable drawable)
        {
            if (drawable is BitmapDrawable)
            {
                BitmapDrawable bitmapDrawable = (BitmapDrawable)drawable;
                if (bitmapDrawable.Bitmap != null)
                {
                    return bitmapDrawable.Bitmap;
                }
            }

            Bitmap bitmap;
            if (drawable.IntrinsicWidth <= 0 || drawable.IntrinsicHeight <= 0)
            {
                bitmap = Bitmap.CreateBitmap(1, 1, Bitmap.Config.Argb8888); // Single color bitmap will be created of 1x1 pixel
            }
            else
            {
                bitmap = Bitmap.CreateBitmap(drawable.IntrinsicWidth, drawable.IntrinsicHeight, Bitmap.Config.Argb8888);
            }

            Canvas canvas = new Canvas(bitmap);
            drawable.SetBounds(0, 0, canvas.Width, canvas.Height);
            drawable.Draw(canvas);
            return bitmap;
        }

        public void RemoveLocalNotification(int scheduleId)
        {
            var notificationManager = NotificationManagerCompat.From(Application.Context);
            notificationManager.Cancel(scheduleId);
        }

        public Task Remove(long scheduleId)
        {
            var pIntent = findIntent(scheduleId);

            if (pIntent != null)
            {
                var alarmManager = (AlarmManager)container.AndroidContext().GetSystemService(Context.AlarmService);
                alarmManager?.Cancel(pIntent);
                pIntent.Cancel();
            }

            return Task.CompletedTask;
        }

        public Task<bool> IsScheduled(long scheduleId)
        {
            var pIntent = findIntent(scheduleId);
            return Task.FromResult(pIntent != null);
        }

        private PendingIntent findIntent(long scheduleId)
        {
            var context = container.AndroidContext();

            var alarmIntent = new Intent(context, typeof(AlarmRingerReceiver));
            alarmIntent.PutExtra("ScheduleId", scheduleId.ToString());

            var pIntent = PendingIntent.GetBroadcast(
                    context,
                    (int)scheduleId,
                    alarmIntent,
                    PendingIntentFlags.NoCreate);

            return pIntent;
        }

        public Task<bool> CanSchedule()
        {
            return Task.FromResult(true);
        }

        public void Dispose()
        {
            storageService.Dispose();
        }
    }

}
