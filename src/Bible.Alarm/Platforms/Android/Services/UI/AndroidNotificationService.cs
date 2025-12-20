using _Microsoft.Android.Resource.Designer;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Media;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Platforms.Android.Services.AndroidServices;
using Bible.Alarm.Platforms.Android.Services.BroadcastReceivers;
using Bible.Alarm.Services.UI.Interfaces;
using Java.Lang;
using Serilog;
using AndroidApplication = Android.App.Application;
using Exception = System.Exception;
using TaskStackBuilder = AndroidX.Core.App.TaskStackBuilder;

namespace Bible.Alarm.Platforms.Android.Services.UI;

public sealed class AndroidNotificationService(ILogger logger) : INotificationService
{
    public static readonly string ChannelIdAndName = "alarm_notification";
    public static readonly string ChannelDescription = "alarm_notification are send to this channel";
    public static readonly string ScheduleId = "schedule_id";


    public async Task ShowNotificationAsync(int scheduleId)
    {
        try
        {
            var context = AndroidApplication.Context;
            var alarmIntent = new Intent(context, typeof(AlarmRingerReceiver));
            alarmIntent.PutExtra("ScheduleId", scheduleId.ToString());
            alarmIntent.PutExtra("IsAlarm", false);

            context.SendBroadcast(alarmIntent);
        }
        catch (Exception e)
        {
            logger.Error(e, "Error happened when playing alarm manually.");
            await Task.Delay(1500);
            throw;
        }
    }

    public async Task ScheduleNotificationAsync(AlarmSchedule schedule,
        string title, string body)
    {
        var time = schedule.NextFireDate();

        try
        {
            if (IsAndroidService())
            {
                AlarmSetupService.ScheduleNotification(AndroidApplication.Context, schedule.Id, time, title, body);
            }
            else
            {
                var intent = new Intent(AndroidApplication.Context, typeof(AlarmSetupService));
                intent.PutExtra("Action", "Add");
                intent.PutExtra("ScheduleId", schedule.Id.ToString());
                intent.PutExtra("Time", time.ToString());
                intent.PutExtra("Title", title);
                intent.PutExtra("Body", body);
                AndroidApplication.Context.StartService(intent);
            }
        }
        catch (SecurityException ex)
        {
            logger.Error(ex, "SecurityException when scheduling alarm for schedule {ScheduleId}. SCHEDULE_EXACT_ALARM permission may be missing or revoked.", schedule.Id);

            // Show user-friendly message
            try
            {
                var toastService = ServiceProviderManager.GetService<IToastService>();
                if (toastService != null)
                {
                    await toastService.ShowMessage(
                        "Cannot schedule reminder. Please enable 'Alarms & reminders' permission in system settings.",
                        7);
                }
            }
            catch (Exception toastEx)
            {
                logger.Warning(toastEx, "Failed to show toast message for exact alarm permission error");
            }

            // Re-throw to be handled by caller
            throw;
        }
    }

    public static void ShowLocalNotification(int scheduleId, string title, string body)
    {
        var notificationManagerCompat = NotificationManagerCompat.From(AndroidApplication.Context);

        // Pass the current button press count value to the next activity:
        var valuesForActivity = new Bundle();
        valuesForActivity.PutInt(ScheduleId, scheduleId);

        var resultIntent = new Intent(AndroidApplication.Context, typeof(MainActivity));
        resultIntent.PutExtras(valuesForActivity);

        var stackBuilder = TaskStackBuilder.Create(AndroidApplication.Context);
        stackBuilder.AddParentStack(Class.FromType(typeof(MainActivity)));
        stackBuilder.AddNextIntent(resultIntent);

        // Create the PendingIntent with the back stack:
        var resultPendingIntent = stackBuilder.GetPendingIntent(0, (int)(PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable));

        var drawable = ContextCompat.GetDrawable(AndroidApplication.Context, ResourceConstant.Drawable.ic_launcher_round);
        var bitmap = DrawableToBitmap(drawable);

        // Build the notification:
        var builder = new NotificationCompat.Builder(AndroidApplication.Context, ChannelIdAndName)
            .SetAutoCancel(true)
            .SetContentIntent(resultPendingIntent)
            .SetContentTitle(title)
            .SetSmallIcon(ResourceConstant.Drawable.exo_icon_circular_play)
            .SetLargeIcon(bitmap)
            .SetContentText(body);

        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            // Use default alarm sound
            var soundUri = RingtoneManager.GetDefaultUri(RingtoneType.Alarm);
            builder.SetSound(soundUri);
            builder.SetDefaults(0);
        }

        notificationManagerCompat.Notify(scheduleId, builder.Build());
    }

    private static Bitmap DrawableToBitmap(Drawable drawable)
    {
        if (drawable is BitmapDrawable)
        {
            var bitmapDrawable = (BitmapDrawable)drawable;
            if (bitmapDrawable.Bitmap != null)
            {
                return bitmapDrawable.Bitmap;
            }
        }

        Bitmap bitmap;
        if (drawable.IntrinsicWidth <= 0 || drawable.IntrinsicHeight <= 0)
        {
            // Single color bitmap will be created of 1x1 pixel
            bitmap = Bitmap.CreateBitmap(1, 1, Bitmap.Config.Argb8888);
        }
        else
        {
            bitmap = Bitmap.CreateBitmap(drawable.IntrinsicWidth, drawable.IntrinsicHeight, Bitmap.Config.Argb8888);
        }

        var canvas = new Canvas(bitmap);
        drawable.SetBounds(0, 0, canvas.Width, canvas.Height);
        drawable.Draw(canvas);
        return bitmap;
    }

    public static void RemoveLocalNotification(int scheduleId)
    {
        var notificationManager = NotificationManagerCompat.From(AndroidApplication.Context);
        notificationManager.Cancel(scheduleId);
    }

    public Task RemoveAsync(int scheduleId)
    {
        var pIntent = FindIntent(scheduleId);

        if (pIntent != null)
        {
            var alarmManager = (AlarmManager)AndroidApplication.Context.GetSystemService(Context.AlarmService);
            alarmManager?.Cancel(pIntent);
            pIntent.Cancel();
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsScheduledAsync(int scheduleId)
    {
        var pIntent = FindIntent(scheduleId);
        return Task.FromResult(pIntent != null);
    }

    private static PendingIntent FindIntent(int scheduleId)
    {
        var context = AndroidApplication.Context;

        var alarmIntent = new Intent(context, typeof(AlarmRingerReceiver));
        alarmIntent.PutExtra("ScheduleId", scheduleId.ToString());

        var pIntent = PendingIntent.GetBroadcast(
            context,
            scheduleId,
            alarmIntent,
            PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable);

        return pIntent;
    }

    public Task<bool> CanScheduleAsync() => Task.FromResult(true);

    private static bool IsAndroidService() => true;
}
