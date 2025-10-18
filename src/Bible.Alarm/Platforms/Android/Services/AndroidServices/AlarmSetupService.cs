using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Bible.Alarm.Droid.Services.Platform;
using Bible.Alarm.Droid.Services.Tasks;
using Bible.Alarm.Services.Droid.Extensions;
using Bible.Alarm.Services.Droid.Helpers;
using Bible.Alarm.Services.Infrastructure;
using Bible.Alarm.Services.Tasks;
using Serilog;
using Bible.Alarm.Platforms.Android;
using static Android.App.AlarmManager;

namespace Bible.Alarm.Services.Droid.Tasks
{
    [Service(Enabled = true)]
    public class AlarmSetupService : Service, IDisposable
    {
        private IContainer _container;
        private static readonly ILogger Logger = Log.ForContext<AlarmSetupService>();


        public static bool IsRunning = false;

        public AlarmSetupService()
        {
            LogSetup.Initialize(VersionFinder.Default,
                new string[] { $"AndroidSdk {Build.VERSION.SdkInt}" }, DevicePlatform.Android.ToString());

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException += UnobserverdTaskException;
        }

        private void UnobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Logger.Error(e.Exception, "Unobserved task exception.");
        }

        private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Error("Unhandled exception.", e.SerializeObject());
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
                _container = BootstrapHelper.InitializeService(this);
            }
            catch (Exception e)
            {
                Logger.Error(e, "An error happenned when initializing sevice in AlarmSetupService.");
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
                            ScheduleNotification(_container.AndroidContext(), long.Parse(intent.GetStringExtra("ScheduleId")), time, title, body);
                            break;
                        }
                    case "SetupBackgroundTasks":
                        BootstrapHelper.VerifyBackgroundTasks(_container.AndroidContext());
                        Task.Run(async () =>
                        {
                            try
                            {
                                using var schedulerTask = _container.Resolve<SchedulerTask>();
                                await schedulerTask.Handle();
                            }
                            catch (Exception e)
                            {
                                Logger.Error(e, "An error happened in handling scheduler task.");
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
                Logger.Error(e, "An error happened in alarm setup task.");
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

        private bool _disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            BootstrapHelper.Remove(this);

            AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException -= UnobserverdTaskException;

            _disposed = true;

            base.Dispose(disposing);
        }
    }
}
