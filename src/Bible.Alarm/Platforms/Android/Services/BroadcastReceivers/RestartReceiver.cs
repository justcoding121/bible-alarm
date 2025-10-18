using Android.App;
using Android.Content;
using Android.OS;
using Bible.Alarm.Droid.Services.Platform;
using Bible.Alarm.Services.Droid.Extensions;
using Bible.Alarm.Services.Droid.Helpers;
using Bible.Alarm.Services.Droid.Tasks;
using Bible.Alarm.Services.Infrastructure;
using Bible.Alarm.Services.Tasks;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Devices;

namespace Bible.Alarm.Droid.Services.Tasks
{

    [BroadcastReceiver(Enabled = true, DirectBootAware = true, Exported = true)]
    [IntentFilter(new[] { Intent.ActionBootCompleted, Intent.ActionLockedBootCompleted,
        "android.intent.action.QUICKBOOT_POWERON", "com.htc.intent.action.QUICKBOOT_POWERON",
        "com.Bible.Alarm.Restart"})]
    public class RestartReceiver : BroadcastReceiver, IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<RestartReceiver>();


        private IContainer _container;
        private Context _context;

        public RestartReceiver()
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

        public override async void OnReceive(Context context, Intent intent)
        {
            _context = context;

            var pendingIntent = GoAsync();

            try
            {
                _container = BootstrapHelper.InitializeService(context);

                BootstrapHelper.VerifyBackgroundTasks(_container.AndroidContext());

                try
                {
                    await BootstrapHelper.VerifyServices(_container);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, $"Failed to process restart task: copy media index. Intent action {intent.Action}");
                }

                using var schedulerTask = _container.Resolve<SchedulerTask>();
                await schedulerTask.Handle();

                context.StopService(intent);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to process restart task. Intent action {intent.Action}");
            }
            finally
            {
                pendingIntent.Finish();
            }
        }

        private bool _disposed = false;
        public new void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _container = null;
                BootstrapHelper.Remove(_context);
            }

            AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException -= UnobserverdTaskException;

            _disposed = true;

            base.Dispose();
        }
    }
}