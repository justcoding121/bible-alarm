using Android.App;
using Android.App.Job;
using Android.Content;
using Android.Media;
using Android.OS;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Services.Droid.Tasks;
using Bible.Alarm.Services.Infrastructure;
using Serilog;

namespace Bible.Alarm.Services.Droid.Helpers;

public class BootstrapHelper
{
    /// <summary>
    /// Main entry point for Android platform initialization
    /// </summary>
    public static void Initialize(ILogger logger, Context context, Android.App.Application application = null)
    {
        // Ensure ServiceProviderManager is initialized for background services
        EnsureServiceProviderInitialized();
        
        // Create notification channel
        CreateNotificationChannel();
        
        // Initialize background tasks
        VerifyBackgroundTasks(context);
        
        // Initialize UI components if application context is provided
        if (application != null)
        {
            InitializeUi(logger, context, application);
        }
    }

    /// <summary>
    /// Verify and initialize all services
    /// </summary>
    public static async Task VerifyServices()
    {
        // Ensure ServiceProviderManager is initialized
        EnsureServiceProviderInitialized();
        await CommonBootstrapHelper.VerifyServices();
    }

    private static void EnsureServiceProviderInitialized()
    {
        if (!ServiceProviderManager.IsInitialized)
        {
            // Initialize minimal DI container for Android background services
            // This should only happen if the main app hasn't started yet
            throw new InvalidOperationException(
                "ServiceProviderManager not initialized. Android background services require the main app to initialize first.");
        }
    }

    private static void InitializeUi(ILogger logger, Context context, Android.App.Application application)
    {
        // MAUI handles platform initialization automatically
        Task.Run(async () =>
        {
            try
            {
                await VerifyServices();
                CreateNotificationChannel();

                Messenger<bool>.Publish(MvvmMessages.Initialized, true);
            }
            catch (Exception e)
            {
                logger.Fatal(e, "Android initialization crashed.");
                throw;
            }
        });
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

        var soundUri = Android.Net.Uri.Parse("android.resource://" + Android.App.Application.Context.PackageName + "/" +
                                             Resource.Raw.cool_alarm_tone_notification_sound);
        // Configure the notification channel.
#pragma warning disable CA1416
        channel.Description = DroidNotificationService.ChannelDescription;
        channel.EnableLights(true);
        channel.EnableVibration(true);
        channel.SetSound(soundUri, attributes);

        var notificationManager =
            (NotificationManager)Android.App.Application.Context.GetSystemService(Context.NotificationService);
        notificationManager.CreateNotificationChannel(channel);
#pragma warning restore CA1416
    }
}