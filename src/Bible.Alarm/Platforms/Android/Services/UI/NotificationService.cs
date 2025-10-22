using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Bible.Alarm.Models;
// using Bible.Alarm.Services.Droid.Extensions; // Removed - no longer needed
using Java.Lang;
using TaskStackBuilder = AndroidX.Core.App.TaskStackBuilder;
using Serilog;
using Android.Graphics.Drawables;
using AndroidX.Core.Content;
using Android.Graphics;
using Bible.Alarm.Contracts.Storage;
using Bible.Alarm.Contracts.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Platforms.Android.Services.AndroidServices;
using Bible.Alarm.Platforms.Android.Services.BroadcastReceivers;
using Bible.Alarm.Platforms.Android;

namespace Bible.Alarm.Platforms.Android.Services.UI;

public class DroidNotificationService(ILogger logger, IStorageService storageService) : INotificationService
{
    public static readonly string ChannelIdAndName = "alarm_notification";
    public static readonly string ChannelDescription = "alarm_notification are send to this channel";
    public static readonly string ScheduleId = "schedule_id";

    private readonly ILogger _logger = logger;


    public async Task ShowNotification(long scheduleId)
    {
        try
        {
            var context = Android.App.Application.Context;
            var alarmIntent = new Intent(context, typeof(AlarmRingerReceiver));
            alarmIntent.PutExtra("ScheduleId", scheduleId.ToString());
            alarmIntent.PutExtra("IsImmediate", true);

            context.SendBroadcast(alarmIntent);
        }
        catch (System.Exception e)
        {
            _logger.Error(e, "Error happened when playing alarm manually.");
            await Task.Delay(1500);
            throw;
        }
    }

    public Task ScheduleNotification(AlarmSchedule schedule,
        string title, string body)
    {
        var time = schedule.NextFireDate();

        if (IsAndroidService())
        {
            AlarmSetupService.ScheduleNotification(Android.App.Application.Context, schedule.Id, time, title, body);
        }
        else
        {
            var intent = new Intent(Android.App.Application.Context, typeof(AlarmSetupService));
            intent.PutExtra("Action", "Add");
            intent.PutExtra("ScheduleId", schedule.Id.ToString());
            intent.PutExtra("Time", time.ToString());
            intent.PutExtra("Title", title);
            intent.PutExtra("Body", body);
            Android.App.Application.Context.StartService(intent);
        }

        return Task.CompletedTask;
    }

    public void ShowLocalNotification(int scheduleId, string title, string body)
    {
        var notificationManagerCompat = NotificationManagerCompat.From(Android.App.Application.Context);

        // Pass the current button press count value to the next activity:
        var valuesForActivity = new Bundle();
        valuesForActivity.PutInt(ScheduleId, scheduleId);

        var resultIntent = new Intent(Android.App.Application.Context, typeof(MainActivity));
        resultIntent.PutExtras(valuesForActivity);

        var stackBuilder = TaskStackBuilder.Create(Android.App.Application.Context);
        stackBuilder.AddParentStack(Class.FromType(typeof(MainActivity)));
        stackBuilder.AddNextIntent(resultIntent);

        // Create the PendingIntent with the back stack:
        var resultPendingIntent = stackBuilder.GetPendingIntent(0, (int)PendingIntentFlags.UpdateCurrent);

        var drawable = ContextCompat.GetDrawable(Android.App.Application.Context, Resource.Drawable.ic_launcher_round);
        var bitmap = DrawableToBitmap(drawable);

        // Build the notification:
        var builder = new NotificationCompat.Builder(Android.App.Application.Context, ChannelIdAndName)
            .SetAutoCancel(true)
            .SetContentIntent(resultPendingIntent)
            .SetContentTitle(title)
            .SetSmallIcon(Resource.Drawable.exo_icon_circular_play)
            .SetLargeIcon(bitmap)
            .SetContentText(body);

        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            var soundUri = Android.Net.Uri.Parse("android.resource://" + Android.App.Application.Context.PackageName +
                                                 "/" + Resource.Raw.cool_alarm_tone_notification_sound);

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
            if (bitmapDrawable.Bitmap != null) return bitmapDrawable.Bitmap;
        }

        Bitmap bitmap;
        if (drawable.IntrinsicWidth <= 0 || drawable.IntrinsicHeight <= 0)
            bitmap = Bitmap.CreateBitmap(1, 1,
                Bitmap.Config.Argb8888); // Single color bitmap will be created of 1x1 pixel
        else
            bitmap = Bitmap.CreateBitmap(drawable.IntrinsicWidth, drawable.IntrinsicHeight, Bitmap.Config.Argb8888);

        var canvas = new Canvas(bitmap);
        drawable.SetBounds(0, 0, canvas.Width, canvas.Height);
        drawable.Draw(canvas);
        return bitmap;
    }

    public void RemoveLocalNotification(int scheduleId)
    {
        var notificationManager = NotificationManagerCompat.From(Android.App.Application.Context);
        notificationManager.Cancel(scheduleId);
    }

    public Task Remove(long scheduleId)
    {
        var pIntent = FindIntent(scheduleId);

        if (pIntent != null)
        {
            var alarmManager = (AlarmManager)Android.App.Application.Context.GetSystemService(Context.AlarmService);
            alarmManager?.Cancel(pIntent);
            pIntent.Cancel();
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsScheduled(long scheduleId)
    {
        var pIntent = FindIntent(scheduleId);
        return Task.FromResult(pIntent != null);
    }

    private PendingIntent FindIntent(long scheduleId)
    {
        var context = Android.App.Application.Context;

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

    private bool IsAndroidService()
    {
        return Android.App.Application.Context != null;
    }

    public void Dispose()
    {
        storageService.Dispose();
    }
}