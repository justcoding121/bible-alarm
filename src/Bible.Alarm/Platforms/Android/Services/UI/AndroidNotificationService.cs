
#nullable disable
using System;
using System.Linq;
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
using Bible.Alarm.Platforms.Android.Services.AndroidServices;
using Bible.Alarm.Platforms.Android.Services.BroadcastReceivers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Java.Lang;
using Serilog;
using AndroidApplication = Android.App.Application;
using Exception = System.Exception;
using TaskStackBuilder = AndroidX.Core.App.TaskStackBuilder;

namespace Bible.Alarm.Platforms.Android.Services.UI;

public sealed class AndroidNotificationService(ILogger logger) : INotificationService
{
    public static readonly string ChannelId = "alarm_notification";
    public static readonly string ChannelName = "Alarm Notifications";
    public static readonly string ChannelDescription = "Notifications for alarms that require you to tap to start playback";
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
        catch (Exception ex)
        {
            await Task.Delay(1500);
            throw new InvalidOperationException("Manual alarm broadcast failed.", ex);
        }
    }

    public async Task ScheduleNotificationAsync(AlarmSchedule alarmSchedule,
        string title, string body)
    {
        var time = alarmSchedule.NextFireDate();

        try
        {
            AlarmSetupService.ScheduleNotification(AndroidApplication.Context, alarmSchedule.Id, time, title, body);
        }
        catch (SecurityException ex)
        {
            try
            {
                var toastService = ServiceProviderManager.GetService<IToastService>();
                if (toastService != null)
                {
                    await toastService.ShowMessage(
                        AppConstants.ToastMessages.CannotScheduleReminderExactAlarmPermission,
                        7);
                }
            }
            catch (Exception toastEx)
            {
                logger.Warning(toastEx, AppConstants.Logging.AndroidNotificationServiceDiagnosticsLog.FailedToShowToastExactAlarmPermissionError);
            }

            throw new InvalidOperationException(
                $"Exact alarm scheduling failed for schedule {alarmSchedule.Id}.",
                ex);
        }
    }

    public static void ShowLocalNotification(int scheduleId, string title, string body)
    {
        var staticLogger = Log.ForContext<AndroidNotificationService>();
        staticLogger.Information("ShowLocalNotification called - ScheduleId={ScheduleId}, Title={Title}, Body={Body}", scheduleId, title, body);

        try
        {
            var notificationManagerCompat = NotificationManagerCompat.From(AndroidApplication.Context);
            if (notificationManagerCompat == null)
            {
                staticLogger.Error("NotificationManagerCompat is null - cannot show notification");
                return;
            }

            EnsureAlarmNotificationChannelExists(staticLogger);

            var resultPendingIntent = CreateTapMainActivityPendingIntent(scheduleId, staticLogger);
            if (resultPendingIntent == null)
            {
                return;
            }

            var drawable = ContextCompat.GetDrawable(AndroidApplication.Context, ResourceConstant.Drawable.ic_launcher_round);
            var bitmap = DrawableToBitmap(drawable);

            var builder = BuildTapToPlayNotification(title, body, resultPendingIntent, bitmap, staticLogger);

            LogNotificationEnablement(staticLogger, notificationManagerCompat);

            var notification = builder.Build();
            notificationManagerCompat.Notify(scheduleId, notification);

            LogNotificationPostedVerification(notificationManagerCompat, scheduleId, staticLogger);
        }
        catch (Exception ex)
        {
            staticLogger.Error(ex, "Error showing local notification - ScheduleId={ScheduleId}", scheduleId);
        }
    }

    private static void EnsureAlarmNotificationChannelExists(ILogger log)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            return;
        }

        var notificationManager = AndroidApplication.Context.GetSystemService(Context.NotificationService) as NotificationManager;
        var channel = notificationManager?.GetNotificationChannel(ChannelId);
        if (channel != null)
        {
            log.Debug("Notification channel {ChannelId} exists with importance {Importance}", ChannelId, channel.Importance);
            return;
        }

        log.Warning("Notification channel {ChannelId} does not exist - creating it now", ChannelId);
        var newChannel = new NotificationChannel(ChannelId, ChannelName, NotificationImportance.Max)
        {
            Description = ChannelDescription
        };
        newChannel.EnableLights(true);
        newChannel.EnableVibration(true);

        var channelSoundUri = RingtoneManager.GetDefaultUri(RingtoneType.Notification);
        var channelAttributes = new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Alarm)
            ?.SetContentType(AudioContentType.Sonification)
            ?.Build();
        newChannel.SetSound(channelSoundUri, channelAttributes);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.NMr1)
        {
            newChannel.SetBypassDnd(true);
        }

        notificationManager?.CreateNotificationChannel(newChannel);
        log.Information("Created notification channel {ChannelId} with sound and alarm audio attributes", ChannelId);
    }

    private static PendingIntent CreateTapMainActivityPendingIntent(int scheduleId, ILogger log)
    {
        var valuesForActivity = new Bundle();
        valuesForActivity.PutInt(ScheduleId, scheduleId);

        var resultIntent = new Intent(AndroidApplication.Context, typeof(MainActivity));
        resultIntent.PutExtras(valuesForActivity);
        resultIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.NewTask);

        var stackBuilder = TaskStackBuilder.Create(AndroidApplication.Context);
        stackBuilder.AddParentStack(Class.FromType(typeof(MainActivity)));
        stackBuilder.AddNextIntent(resultIntent);

        var resultPendingIntent = stackBuilder.GetPendingIntent(0, (int)(PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable));
        if (resultPendingIntent == null)
        {
            log.Error("Failed to create PendingIntent - cannot show notification");
        }

        return resultPendingIntent;
    }

    private static NotificationCompat.Builder BuildTapToPlayNotification(
        string title,
        string body,
        PendingIntent contentIntent,
        Bitmap largeIcon,
        ILogger log)
    {
        var builder = new NotificationCompat.Builder(AndroidApplication.Context, ChannelId)
            .SetAutoCancel(true)
            .SetContentIntent(contentIntent)
            .SetContentTitle(title)
            .SetSmallIcon(ResourceConstant.Drawable.exo_icon_circular_play)
            .SetLargeIcon(largeIcon)
            .SetContentText(body)
            .SetPriority(NotificationCompat.PriorityMax)
            .SetVisibility(NotificationCompat.VisibilityPublic)
            .SetCategory(NotificationCompat.CategoryAlarm)
            .SetShowWhen(true)
            .SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis());

        var soundUri = RingtoneManager.GetDefaultUri(RingtoneType.Notification);

        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            builder.SetSound(soundUri);
            builder.SetDefaults(NotificationCompat.DefaultAll);
        }
        else
        {
            builder.SetSound(soundUri);
            log.Debug("Set notification sound explicitly for Android O+ - SoundUri={SoundUri}, channel audio attributes will be used", soundUri);
        }

        return builder;
    }

    private static void LogNotificationEnablement(ILogger log, NotificationManagerCompat notificationManagerCompat)
    {
        var areNotificationsEnabled = notificationManagerCompat.AreNotificationsEnabled();
        log.Information("Notifications enabled for app: {AreNotificationsEnabled}", areNotificationsEnabled);

        if (!areNotificationsEnabled)
        {
            log.Warning("Notifications are disabled for this app - notification will not be shown. User needs to enable notifications in system settings.");
        }
    }

    private static void LogNotificationPostedVerification(NotificationManagerCompat notificationManagerCompat, int scheduleId, ILogger log)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            var activeNotifications = notificationManagerCompat.ActiveNotifications;
            var wasPosted = activeNotifications?.Any(n => n.Id == scheduleId) ?? false;
            log.Information("Notification posted - ScheduleId={ScheduleId}, NotificationId={NotificationId}, WasActuallyPosted={WasPosted}, ActiveNotificationCount={Count}",
                scheduleId, scheduleId, wasPosted, activeNotifications?.Count ?? 0);

            if (!wasPosted)
            {
                log.Warning("Notification was not actually posted to system - may be blocked by system settings or app permissions");
            }
        }
        else
        {
            log.Information("Local notification shown successfully - ScheduleId={ScheduleId}, NotificationId={NotificationId}", scheduleId, scheduleId);
        }
    }

    private static Bitmap DrawableToBitmap(Drawable drawable)
    {
        if (drawable is BitmapDrawable bitmapDrawable && bitmapDrawable.Bitmap != null)
        {
            return bitmapDrawable.Bitmap;
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

    /// <summary>
    /// Checks if a local notification is currently active (visible to user).
    /// </summary>
    public static Task<bool> IsLocalNotificationActiveAsync(int scheduleId)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            try
            {
                var notificationManager = NotificationManagerCompat.From(AndroidApplication.Context);
                var activeNotifications = notificationManager.ActiveNotifications;
                if (activeNotifications != null)
                {
                    foreach (var notification in activeNotifications)
                    {
                        if (notification.Id == scheduleId)
                        {
                            return Task.FromResult(true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var staticLogger = Log.ForContext<AndroidNotificationService>();
                staticLogger.Warning(ex, "Error checking if notification {ScheduleId} is active", scheduleId);
            }
        }

        // For older Android versions or if check fails, assume notification doesn't exist
        // This is safe because worst case we'll show notification again (harmless)
        return Task.FromResult(false);
    }

    public Task ClearDeliveredNotificationAsync(int scheduleId)
    {
        RemoveLocalNotification(scheduleId);
        return Task.CompletedTask;
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
}
