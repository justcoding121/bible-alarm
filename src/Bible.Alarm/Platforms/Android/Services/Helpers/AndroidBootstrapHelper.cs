using Android.App;
using Android.App.Job;
using Android.Content;
using Android.Media;
using Android.OS;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Platforms.Android.Services.Jobs;
using Bible.Alarm.Platforms.Android.Services.UI;
using Serilog;
using AndroidApplication = Android.App.Application;

namespace Bible.Alarm.Platforms.Android.Services.Helpers;

public static class AndroidBootstrapHelper
{
    /// <summary>
    /// Main entry point for Android platform initialization
    /// </summary>
    public static async Task Initialize(ILogger logger, Context context, AndroidApplication application = null, bool isForeground = false)
    {
        logger.Information("AndroidBootstrapHelper.Initialize called with isForeground={IsForeground}", isForeground);

        try
        {
            // Pass isForeground to VerifyServices so InitializedMessage is sent for foreground launches
            // Database operations run in background Task.Run, so UI thread is not blocked
            await CommonBootstrapHelper.VerifyServices(isForeground);
            logger.Information("Android database initialization completed successfully.");
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                "Android database initialization crashed.",
                e);
        }

        CreateNotificationChannel();

        VerifyBackgroundTasks(context);
    }

    private static void VerifyBackgroundTasks(Context context)
    {
        SchedulerSetupTask(context);
    }

    private static void SchedulerSetupTask(Context context)
    {
        using var jobBuilder = context.CreateJobBuilderUsingJobId<SchedulerJob>(SchedulerJob.JobId, 30);
        var jobInfo = jobBuilder.Build();

        var jobScheduler = (JobScheduler)context.GetSystemService(Context.JobSchedulerService);
        if (jobScheduler == null)
        {
            return;
        }

        if (jobInfo == null)
        {
            return;
        }

        _ = jobScheduler.Schedule(jobInfo);
    }



    private static void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            // Notification channels are new in API 26 (and not a part of the
            // support library). There is no need to create a notification
            // channel on older versions of Android.
            return;
        }

        var channelId = AndroidNotificationService.ChannelId;
        var channelName = AndroidNotificationService.ChannelName;
        var channelDescription = AndroidNotificationService.ChannelDescription;

        // Use Max importance for alarm notifications to ensure they bypass Do Not Disturb
        // and have highest priority
        var channel = new NotificationChannel(channelId, channelName, NotificationImportance.Max)
        {
            Description = channelDescription
        };

        // Use Alarm usage for alarm notifications to ensure they play even in silent/DND mode
        // and have higher audio priority
        var attributes = new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Alarm)
            ?.SetContentType(AudioContentType.Sonification)
            ?.Build();

        // Use default notification sound (short message tone/alert) for tap-to-play notifications
        // This ensures a short alert sound instead of a long ringtone when tap is enabled
        var soundUri = RingtoneManager.GetDefaultUri(RingtoneType.Notification);

        channel.Description = AndroidNotificationService.ChannelDescription;
        channel.EnableLights(true);
        channel.EnableVibration(true);
        channel.SetSound(soundUri, attributes);

        // Allow notifications to bypass Do Not Disturb mode (Android 7.1+)
        // This ensures alarms can play even when DND is enabled
        if (Build.VERSION.SdkInt >= BuildVersionCodes.NMr1)
        {
            channel.SetBypassDnd(true);
        }

        var notificationManager =
            (NotificationManager)AndroidApplication.Context.GetSystemService(Context.NotificationService);
        notificationManager?.CreateNotificationChannel(channel);
    }
}
