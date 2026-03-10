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
            AlarmSetupService.ScheduleNotification(AndroidApplication.Context, schedule.Id, time, title, body);
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

            // Ensure notification channel exists (should be created during bootstrap, but verify)
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var notificationManager = AndroidApplication.Context.GetSystemService(Context.NotificationService) as NotificationManager;
                var channel = notificationManager?.GetNotificationChannel(ChannelId);
                if (channel == null)
                {
                    staticLogger.Warning("Notification channel {ChannelId} does not exist - creating it now", ChannelId);
                    // Channel should have been created during bootstrap, but create it if missing
                    var newChannel = new NotificationChannel(ChannelId, ChannelName, NotificationImportance.Max)
                    {
                        Description = ChannelDescription
                    };
                    newChannel.EnableLights(true);
                    newChannel.EnableVibration(true);

                    // Use default notification sound (short message tone/alert) for tap-enabled alarms
                    var channelSoundUri = RingtoneManager.GetDefaultUri(RingtoneType.Notification);
                    var channelAttributes = new AudioAttributes.Builder()
                        .SetUsage(AudioUsageKind.Alarm) // Use Alarm to ensure it plays even in silent/DND mode
                        ?.SetContentType(AudioContentType.Sonification)
                        ?.Build();
                    newChannel.SetSound(channelSoundUri, channelAttributes);

                    // Allow notifications to bypass Do Not Disturb mode (Android 7.1+)
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.NMr1)
                    {
                        newChannel.SetBypassDnd(true);
                    }

                    notificationManager?.CreateNotificationChannel(newChannel);
                    staticLogger.Information("Created notification channel {ChannelId} with sound and alarm audio attributes", ChannelId);
                }
                else
                {
                    staticLogger.Debug("Notification channel {ChannelId} exists with importance {Importance}", ChannelId, channel.Importance);
                }
            }

            // Pass the current button press count value to the next activity:
            var valuesForActivity = new Bundle();
            valuesForActivity.PutInt(ScheduleId, scheduleId);

            var resultIntent = new Intent(AndroidApplication.Context, typeof(MainActivity));
            resultIntent.PutExtras(valuesForActivity);
            // Set flags to bring app to foreground when notification is tapped
            resultIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.NewTask);

            var stackBuilder = TaskStackBuilder.Create(AndroidApplication.Context);
            stackBuilder.AddParentStack(Class.FromType(typeof(MainActivity)));
            stackBuilder.AddNextIntent(resultIntent);

            // Create the PendingIntent with the back stack:
            var resultPendingIntent = stackBuilder.GetPendingIntent(0, (int)(PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable));
            if (resultPendingIntent == null)
            {
                staticLogger.Error("Failed to create PendingIntent - cannot show notification");
                return;
            }

            var drawable = ContextCompat.GetDrawable(AndroidApplication.Context, ResourceConstant.Drawable.ic_launcher_round);
            var bitmap = DrawableToBitmap(drawable);

            // Build the notification:
            var builder = new NotificationCompat.Builder(AndroidApplication.Context, ChannelId)
                .SetAutoCancel(true)
                .SetContentIntent(resultPendingIntent)
                .SetContentTitle(title)
                .SetSmallIcon(ResourceConstant.Drawable.exo_icon_circular_play)
                .SetLargeIcon(bitmap)
                .SetContentText(body)
                .SetPriority(NotificationCompat.PriorityMax) // Maximum priority to ensure visibility
                .SetVisibility(NotificationCompat.VisibilityPublic) // Show on lock screen
                .SetCategory(NotificationCompat.CategoryAlarm) // Mark as alarm category
                .SetShowWhen(true) // Show timestamp
                .SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis()) // Set current time
                .SetFullScreenIntent(resultPendingIntent, false); // Don't show full screen, but allow heads-up

            // Use default notification sound (short message tone/alert) for tap-enabled alarms
            // This ensures a short alert sound instead of a long ringtone
            var soundUri = RingtoneManager.GetDefaultUri(RingtoneType.Notification);

            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            {
                // For pre-O Android, explicitly set the notification sound and defaults
                builder.SetSound(soundUri);
                builder.SetDefaults(NotificationCompat.DefaultAll); // Enable sound, vibration, lights for pre-O
            }
            else
            {
                // For Android O+, explicitly set sound on notification to ensure it plays
                // The channel's audio attributes (Alarm usage) will be used automatically
                // Explicitly setting sound ensures the notification plays even if channel settings change
                builder.SetSound(soundUri);
                staticLogger.Debug("Set notification sound explicitly for Android O+ - SoundUri={SoundUri}, channel audio attributes will be used", soundUri);
            }

            // Check if notifications are enabled for this app
            var areNotificationsEnabled = notificationManagerCompat.AreNotificationsEnabled();
            staticLogger.Information("Notifications enabled for app: {AreNotificationsEnabled}", areNotificationsEnabled);

            if (!areNotificationsEnabled)
            {
                staticLogger.Warning("Notifications are disabled for this app - notification will not be shown. User needs to enable notifications in system settings.");
            }

            var notification = builder.Build();

            // Force heads-up notification by using a high-priority notification ID and ensuring it's not silent
            // On Android 13+, we need to explicitly request heads-up behavior
            notificationManagerCompat.Notify(scheduleId, notification);

            // Verify notification was actually posted
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                var activeNotifications = notificationManagerCompat.ActiveNotifications;
                var wasPosted = activeNotifications?.Any(n => n.Id == scheduleId) ?? false;
                staticLogger.Information("Notification posted - ScheduleId={ScheduleId}, NotificationId={NotificationId}, WasActuallyPosted={WasPosted}, ActiveNotificationCount={Count}",
                    scheduleId, scheduleId, wasPosted, activeNotifications?.Count ?? 0);

                if (!wasPosted)
                {
                    staticLogger.Warning("Notification was not actually posted to system - may be blocked by system settings or app permissions");
                }
            }
            else
            {
                staticLogger.Information("Local notification shown successfully - ScheduleId={ScheduleId}, NotificationId={NotificationId}", scheduleId, scheduleId);
            }
        }
        catch (Exception ex)
        {
            staticLogger.Error(ex, "Error showing local notification - ScheduleId={ScheduleId}", scheduleId);
        }
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
