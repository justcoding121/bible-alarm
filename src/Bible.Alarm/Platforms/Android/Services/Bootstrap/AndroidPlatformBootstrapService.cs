#nullable enable

using Android.App;
using Android.App.Job;
using Android.Content;
using Android.Media;
using Android.OS;
using Bible.Alarm.Platforms.Android.Services.Helpers;
using Bible.Alarm.Platforms.Android.Services.Jobs;
using Bible.Alarm.Platforms.Android.Services.UI;
using Bible.Alarm.Services.Bootstrap.Interfaces;
using Serilog;
using AndroidApplication = Android.App.Application;

namespace Bible.Alarm.Platforms.Android.Services.Bootstrap;

/// <summary>
/// Android-specific platform initialization service.
/// Creates notification channels and schedules background jobs.
/// </summary>
public class AndroidPlatformBootstrapService : IPlatformBootstrapService
{
    private static readonly ILogger logger = Log.ForContext<AndroidPlatformBootstrapService>();
    private static volatile bool initialized;
    private static readonly object initLock = new();

    public Task InitializeAsync()
    {
        // Quick check without lock for performance
        if (initialized)
        {
            logger.Debug("Android platform already initialized, skipping");
            return Task.CompletedTask;
        }

        lock (initLock)
        {
            // Double-check inside lock
            if (initialized)
            {
                return Task.CompletedTask;
            }

            try
            {
                logger.Information("Initializing Android platform services");

                // Create notification channel for alarms
                CreateNotificationChannel();

                // Schedule background jobs
                // Use fully qualified namespace to avoid conflicts with local Platform namespace
                var context = global::Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.ApplicationContext
                    ?? AndroidApplication.Context;
                VerifyBackgroundTasks(context);

                initialized = true;
                logger.Information("Android platform services initialized successfully");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error initializing Android platform services");
                // Don't throw - platform init failure is non-fatal
            }
        }

        return Task.CompletedTask;
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
            (NotificationManager?)AndroidApplication.Context.GetSystemService(Context.NotificationService);
        notificationManager?.CreateNotificationChannel(channel);

        logger.Debug("Created alarm notification channel: {ChannelId}", channelId);
    }

    private static void VerifyBackgroundTasks(Context context)
    {
        SchedulerSetupTask(context);
    }

    private static void SchedulerSetupTask(Context context)
    {
        using var jobBuilder = context.CreateJobBuilderUsingJobId<SchedulerJob>(SchedulerJob.JobId, 30);
        var jobInfo = jobBuilder.Build();

        var jobScheduler = (JobScheduler?)context.GetSystemService(Context.JobSchedulerService);
        if (jobScheduler == null || jobInfo == null)
        {
            logger.Warning("Failed to schedule SchedulerJob - JobScheduler or JobInfo is null");
            return;
        }

        var scheduleResult = jobScheduler.Schedule(jobInfo);
        var success = JobScheduler.ResultSuccess == scheduleResult;

        if (success)
        {
            logger.Debug("SchedulerJob scheduled successfully");
        }
        else
        {
            logger.Warning("Failed to schedule SchedulerJob - result: {Result}", scheduleResult);
        }
    }

}
