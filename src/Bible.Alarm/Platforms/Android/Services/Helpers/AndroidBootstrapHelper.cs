using _Microsoft.Android.Resource.Designer;
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
using AndroidNet = Android.Net;

namespace Bible.Alarm.Platforms.Android.Services.Helpers;

public class AndroidBootstrapHelper
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
        using var jobBuilder = context.CreateJobBuilderUsingJobId<SchedulerJob>(SchedulerJob.JobId, 30);
        var jobInfo = jobBuilder.Build();

        var jobScheduler = (JobScheduler)context.GetSystemService(Context.JobSchedulerService);
        if (jobScheduler == null)
        {
            return false;
        }

        if (jobInfo == null)
        {
            return false;
        }

        var scheduleResult = jobScheduler.Schedule(jobInfo);

        return JobScheduler.ResultSuccess == scheduleResult;
    }


    private static bool UpdateMediaIndexJobTask(Context context)
    {
        using var jobBuilder = context.CreateJobBuilderUsingJobId<UpdateMediaIndexJob>(UpdateMediaIndexJob.JobId, 60);
        var jobInfo = jobBuilder.Build();

        var jobScheduler = (JobScheduler)context.GetSystemService(Context.JobSchedulerService);
        if (jobScheduler == null)
        {
            return false;
        }

        if (jobInfo == null)
        {
            return false;
        }

        var scheduleResult = jobScheduler.Schedule(jobInfo);

        return JobScheduler.ResultSuccess == scheduleResult;

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

        var channelId = AndroidNotificationService.ChannelIdAndName;
        var channelName = AndroidNotificationService.ChannelIdAndName;
        var channelDescription = AndroidNotificationService.ChannelDescription;

        var channel = new NotificationChannel(channelId, channelName, NotificationImportance.High)
        {
            Description = channelDescription
        };

        var attributes = new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Notification)
            ?.SetContentType(AudioContentType.Sonification)
            ?.Build();

        // Use default alarm sound
        var soundUri = RingtoneManager.GetDefaultUri(RingtoneType.Alarm);

        channel.Description = AndroidNotificationService.ChannelDescription;
        channel.EnableLights(true);
        channel.EnableVibration(true);
        channel.SetSound(soundUri, attributes);

        var notificationManager =
            (NotificationManager)AndroidApplication.Context.GetSystemService(Context.NotificationService);
        notificationManager?.CreateNotificationChannel(channel);
    }
}
