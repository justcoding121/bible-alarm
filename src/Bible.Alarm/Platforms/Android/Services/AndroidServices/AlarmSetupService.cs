using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Bible.Alarm.Droid;
using Bible.Alarm.Droid.Services.Platform;
using Bible.Alarm.Droid.Services.Tasks;
using Bible.Alarm.Services.Droid.Helpers;
using Bible.Alarm.Services.Infrastructure;
using Bible.Alarm.Services.Tasks;
using Newtonsoft.Json;
using NLog;
using System;
using System.Threading.Tasks;
using Xamarin.Forms;
using static Android.App.AlarmManager;

namespace Bible.Alarm.Services.Droid.Tasks
{
    [Service(Enabled = true)]
    public class AlarmSetupService : Service, IDisposable
    {
        private IContainer container;
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;


        public static bool IsRunning = false;

        public AlarmSetupService()
        {
            LogSetup.Initialize(VersionFinder.Default,
                new string[] { $"AndroidSdk {Build.VERSION.SdkInt}" }, Device.Android);

            AppDomain.CurrentDomain.UnhandledException += unhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException += unobserverdTaskException;
        }

        private void unobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            logger.Error(e.Exception, "Unobserved task exception.");
        }

        private void unhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            logger.Error("Unhandled exception.", e.SerializeObject());
        }

        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        public override void OnCreate()
        {
            base.OnCreate();
            IsRunning = true;
        }

        [return: GeneratedEnum]
        public override StartCommandResult OnStartCommand(Intent intent, [GeneratedEnum] StartCommandFlags flags, int startId)
        {

            try
            {
                this.container = BootstrapHelper.InitializeService(this);
            }
            catch (Exception e)
            {
                logger.Error(e, "An error happenned when initializing sevice in AlarmSetupService.");
            }

            try
            {

                var extra = intent.GetStringExtra("Action");

                switch (extra)
                {
                    case "Add":
                        {
                            var time = DateTimeOffset.Parse(intent.GetStringExtra("Time"));
                            var title = intent.GetStringExtra("Title");
                            var body = intent.GetStringExtra("Body");
                            ScheduleNotification(container.AndroidContext(), long.Parse(intent.GetStringExtra("ScheduleId")), time, title, body);
                            break;
                        }
                    case "SetupBackgroundTasks":
                        BootstrapHelper.VerifyBackgroundTasks(container.AndroidContext());
                        Task.Run(async () =>
                        {
                            try
                            {
                                using var schedulerTask = container.Resolve<SchedulerTask>();
                                await schedulerTask.Handle();
                            }
                            catch (Exception e)
                            {
                                logger.Error(e, "An error happened in handling scheduler task.");
                            }
                        });
                        break;
                    default:
                        throw new NotImplementedException();
                }

                StopSelf();

                return base.OnStartCommand(intent, flags, startId);
            }
            catch (Exception e)
            {
                logger.Error(e, "An error happened in alarm setup task.");
                throw;
            }
        }

        public override void OnDestroy()
        {
            IsRunning = false;
        }

        public static void ScheduleNotification(Context context, long scheduleId, DateTimeOffset time,
            string title, string body)
        {
            using var alarmIntent = new Intent(context, typeof(AlarmRingerReceiver));
            alarmIntent.PutExtra("ScheduleId", scheduleId.ToString());

            using var pIntent = PendingIntent.GetBroadcast(
                     context,
                     (int)scheduleId,
                     alarmIntent,
                     PendingIntentFlags.UpdateCurrent);
            using var alarmService = (AlarmManager)context.GetSystemService(Context.AlarmService);

            // Figure out the alaram in milliseconds.
            var milliSecondsRemaining = Java.Lang.JavaSystem.CurrentTimeMillis()
                + (long)time.Subtract(DateTimeOffset.Now).TotalSeconds * 1000;

            if (Build.VERSION.SdkInt < BuildVersionCodes.M)
            {
                alarmService.SetExact(AlarmType.RtcWakeup, milliSecondsRemaining, pIntent);
            }
            else
            {
                using (var mainLauncherIntent = new Intent(context, typeof(SplashActivity)))
                {
                    mainLauncherIntent.SetFlags(ActivityFlags.ReorderToFront);

                    var mainLauncherPendingIntent = PendingIntent.GetActivity(
                       context,
                       0,
                       mainLauncherIntent,
                       PendingIntentFlags.UpdateCurrent);

                    alarmService.SetAlarmClock(new AlarmClockInfo(milliSecondsRemaining, mainLauncherPendingIntent), pIntent);
                }
            }
        }

        private bool disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            BootstrapHelper.Remove(this);

            AppDomain.CurrentDomain.UnhandledException -= unhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException -= unobserverdTaskException;

            disposed = true;

            base.Dispose(disposing);
        }
    }
}
