using Android.App;
using Android.App.Job;
using Android.Content;
using Android.Media;
using Android.OS;
using AndroidApplication = global::Android.App.Application;
using AndroidNet = global::Android.Net;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Platforms.Android.Services.Jobs;
using Bible.Alarm.Platforms.Android.Services.UI;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Helpers;

public class BootstrapHelper
{
    /// <summary>
    /// Main entry point for Android platform initialization
    /// </summary>
    public static void Initialize(ILogger logger, Context context, AndroidApplication application = null)
    {
        // Initialize database and services (both foreground and background)
        // This must complete before any other tasks
        try
        {
            CommonBootstrapHelper.VerifyServices().Wait();
            logger.Information("Android database initialization completed successfully.");
        }
        catch (Exception e)
        {
            logger.Fatal(e, "Android database initialization crashed.");
            throw;
        }
        
        // Create notification channel
        CreateNotificationChannel();
        
        // Initialize background tasks
        VerifyBackgroundTasks(context);
    }

    private static void VerifyBackgroundTasks(Context context)
    {
        SchedulerSetupTask(context);
        UpdateMediaIndexJobTask(context);
    }

    private static bool SchedulerSetupTask(Context context)
    {
        // Sample usage - creates a JobBuilder for a SchedulerJob and sets the Job ID to 1.
        using var jobBuilder = context.CreateJobBuilderUsingJobId<SchedulerJob>(SchedulerJob.JobId, 30);
        var jobInfo = jobBuilder.Build(); // creates a JobInfo object.

        var jobScheduler = (JobScheduler)context.GetSystemService(Context.JobSchedulerService);
        var scheduleResult = jobScheduler.Schedule(jobInfo);

        if (JobScheduler.ResultSuccess == scheduleResult) return true;

        return false;
    }


    private static bool UpdateMediaIndexJobTask(Context context)
    {
        // Sample usage - creates a JobBuilder for a SchedulerJob and sets the Job ID to 2.
        using var jobBuilder = context.CreateJobBuilderUsingJobId<UpdateMediaIndexJob>(UpdateMediaIndexJob.JobId, 60);
        var jobInfo = jobBuilder.Build(); // creates a JobInfo object.

        var jobScheduler = (JobScheduler)context.GetSystemService(Context.JobSchedulerService);
        var scheduleResult = jobScheduler.Schedule(jobInfo);

        if (JobScheduler.ResultSuccess == scheduleResult) return true;

        return false;
    }

    private static void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            // Notification channels are new in API 26 (and not a part of the
            // support library). There is no need to create a notification
            // channel on older versions of Android.
            return;

        var channelId = DroidNotificationService.ChannelIdAndName;
        var channelName = DroidNotificationService.ChannelIdAndName;
        var channelDescription = DroidNotificationService.ChannelDescription;
#pragma warning disable CA1416
        var channel = new NotificationChannel(channelId, channelName, NotificationImportance.High)
        {
            Description = channelDescription
        };
#pragma warning restore CA1416

        var attributes = new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Notification)
            .SetContentType(AudioContentType.Sonification)
            .Build();

        var soundUri = AndroidNet.Uri.Parse("android.resource://" + AndroidApplication.Context.PackageName + "/" +
                                             Resource.Raw.cool_alarm_tone_notification_sound);
        // Configure the notification channel.
#pragma warning disable CA1416
        channel.Description = DroidNotificationService.ChannelDescription;
        channel.EnableLights(true);
        channel.EnableVibration(true);
        channel.SetSound(soundUri, attributes);

        var notificationManager =
            (NotificationManager)AndroidApplication.Context.GetSystemService(Context.NotificationService);
        notificationManager.CreateNotificationChannel(channel);
#pragma warning restore CA1416
    }
}