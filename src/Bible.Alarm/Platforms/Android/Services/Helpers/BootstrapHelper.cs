using Android;
using Android.App;
using Android.App.Job;
using Android.Content;
using Android.Content.Res;
using Android.Media;
using Android.OS;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Services.Droid.Tasks;
using Bible.Alarm.Services.Contracts;
using Microsoft.EntityFrameworkCore;
using NLog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Bible.Alarm.Services.Droid.Helpers
{
    public class BootstrapHelper
    {
        public static IContainer GetInitializedContainer()
        {
            return Bible.Alarm.Services.Droid.IocSetup.GetContainer();
        }

        public static IContainer InitializeService(Context context)
        {
            var result = Bible.Alarm.Services.Droid.IocSetup.Initialize(context, true);
            var container = result.Item1;
            var containerCreated = result.Item2;
            if (containerCreated)
            {
                var application = (Android.App.Application)context.ApplicationContext;
                // MAUI handles platform initialization automatically
                createNotificationChannel(container);
            }

            return result.Item1;
        }

        public static IContainer InitializeUI(Logger logger, Context context, Android.App.Application application)
        {
            var result = Bible.Alarm.Services.Droid.IocSetup.Initialize(context, false);
            var container = result.Item1;

            var containerCreated = result.Item2;
            if (containerCreated)
            {
                // MAUI handles platform initialization automatically

                Task.Run(async () =>
                {
                    try
                    {
                        await VerifyServices(container);
                        createNotificationChannel(container);

                        Messenger<bool>.Publish(MvvmMessages.Initialized, true);

                    }
                    catch (Exception e)
                    {
                        logger.Fatal(e, "Android initialization crashed.");
                        throw;
                    }
                });

            }
            else
            {
                Task.Run(() =>
                {
                    try
                    {
                        Messenger<bool>.Publish(MvvmMessages.Initialized, true);
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, "An error happened bootstrap helper Messenger publish.");
                    }
                });
            }

            return container;
        }

        public async static Task VerifyServices(IContainer container)
        {
            await CommonBootstrapHelper.VerifyServices(container);
        }

        internal static void Remove(Context context)
        {
            Bible.Alarm.Services.Droid.IocSetup.Remove(context);
        }

        public static void VerifyBackgroundTasks(Context context)
        {
            schedulerSetupTask(context);
            updateMediaIndexJobTask(context);
        }

        private static bool schedulerSetupTask(Context context)
        {
            // Sample usage - creates a JobBuilder for a SchedulerJob and sets the Job ID to 1.
            using var jobBuilder = context.CreateJobBuilderUsingJobId<SchedulerJob>(SchedulerJob.JobId, 30);
            var jobInfo = jobBuilder.Build();  // creates a JobInfo object.

            var jobScheduler = (JobScheduler)context.GetSystemService(Context.JobSchedulerService);
            var scheduleResult = jobScheduler.Schedule(jobInfo);

            if (JobScheduler.ResultSuccess == scheduleResult)
            {
                return true;
            }

            return false;
        }


        private static bool updateMediaIndexJobTask(Context context)
        {
            // Sample usage - creates a JobBuilder for a SchedulerJob and sets the Job ID to 2.
            using var jobBuilder = context.CreateJobBuilderUsingJobId<UpdateMediaIndexJob>(UpdateMediaIndexJob.JobId, 60);
            var jobInfo = jobBuilder.Build();  // creates a JobInfo object.

            var jobScheduler = (JobScheduler)context.GetSystemService(Context.JobSchedulerService);
            var scheduleResult = jobScheduler.Schedule(jobInfo);

            if (JobScheduler.ResultSuccess == scheduleResult)
            {
                return true;
            }

            return false;
        }

        private static void createNotificationChannel(IContainer container)
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            {
                // Notification channels are new in API 26 (and not a part of the
                // support library). There is no need to create a notification
                // channel on older versions of Android.
                return;
            }

            var channelId = DroidNotificationService.CHANNEL_ID_AND_NAME;
            var channelName = DroidNotificationService.CHANNEL_ID_AND_NAME;
            var channelDescription = DroidNotificationService.CHANNEL_DESCRIPTION;
            var channel = new NotificationChannel(channelId, channelName, NotificationImportance.High)
            {
                Description = channelDescription
            };

            var attributes = new AudioAttributes.Builder()
                    .SetUsage(AudioUsageKind.Notification)
                    .SetContentType(AudioContentType.Sonification)
                    .Build();

            var soundUri = Android.Net.Uri.Parse("android.resource://" + Application.Context.PackageName + "/" + Resource.Raw.cool_alarm_tone_notification_sound);
            // Configure the notification channel.
            channel.Description = DroidNotificationService.CHANNEL_DESCRIPTION;
            channel.EnableLights(true);
            channel.EnableVibration(true);
            channel.SetSound(soundUri, attributes);

            var notificationManager = (NotificationManager)Application.Context.GetSystemService(Context.NotificationService);
            notificationManager.CreateNotificationChannel(channel);
        }
    }
}
