using Android.App;
using Android.Content;
using Android.OS;
using Bible.Alarm.Droid.Services.Platform;
using Bible.Alarm.Services.Droid.Helpers;
using Bible.Alarm.Services.Droid.Tasks;
using Bible.Alarm.Services.Infrastructure;
using Bible.Alarm.Services.Tasks;
using Newtonsoft.Json;
using NLog;
using System;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace Bible.Alarm.Droid.Services.Tasks
{

    [BroadcastReceiver(Enabled = true, DirectBootAware = true, Exported = true)]
    [IntentFilter(new[] { Intent.ActionBootCompleted, Intent.ActionLockedBootCompleted,
        "android.intent.action.QUICKBOOT_POWERON", "com.htc.intent.action.QUICKBOOT_POWERON",
        "com.Bible.Alarm.Restart"})]
    public class RestartReceiver : BroadcastReceiver, IDisposable
    {
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;


        private IContainer container;
        private Context context;

        public RestartReceiver()
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

        public override async void OnReceive(Context context, Intent intent)
        {
            this.context = context;

            var pendingIntent = GoAsync();

            try
            {
                this.container = BootstrapHelper.InitializeService(context);

                BootstrapHelper.VerifyBackgroundTasks(container.AndroidContext());

                try
                {
                    await BootstrapHelper.VerifyServices(container);
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, $"Failed to process restart task: copy media index. Intent action {intent.Action}");
                }

                using var schedulerTask = container.Resolve<SchedulerTask>();
                await schedulerTask.Handle();

                context.StopService(intent);
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to process restart task. Intent action {intent.Action}");
            }
            finally
            {
                pendingIntent.Finish();
            }
        }

        private bool disposed = false;
        public new void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                container = null;
                BootstrapHelper.Remove(context);
            }

            AppDomain.CurrentDomain.UnhandledException -= unhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException -= unobserverdTaskException;

            disposed = true;

            base.Dispose();
        }
    }
}