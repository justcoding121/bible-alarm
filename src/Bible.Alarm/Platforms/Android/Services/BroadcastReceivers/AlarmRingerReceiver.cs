using Android.App;
using Android.Content;
using Android.OS;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Droid.Services.Handlers;
using Bible.Alarm.Droid.Services.Platform;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.Services.Droid.Helpers;
using Bible.Alarm.Services.Infrastructure;
using MediaManager;
using MediaManager.Player;
using Newtonsoft.Json;
using NLog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bible.Alarm.Droid.Services.Tasks
{
    [BroadcastReceiver(Enabled = true)]
    public class AlarmRingerReceiver : BroadcastReceiver, IDisposable
    {
        private static readonly Lazy<Logger> LazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger Logger => LazyLogger.Value;


        private IContainer _container;
        private Context _context;
        private Intent _intent;
        private AndroidAlarmHandler _alarmHandler;

        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1);
        public AlarmRingerReceiver()
        {
            LogSetup.Initialize(VersionFinder.Default,
                new string[] { $"AndroidSdk {Android.OS.Build.VERSION.SdkInt}" }, "Android");

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
            var pendingIntent = GoAsync();

            await Lock.WaitAsync();

            try
            {
                _container = BootstrapHelper.InitializeService(context);

                _context = context;
                _intent = intent;

                var scheduleId = intent.GetStringExtra("ScheduleId");
                var isImmediate = intent.GetBooleanExtra("IsImmediate", false);

                _alarmHandler = _container.Resolve<AndroidAlarmHandler>();
                _alarmHandler.Disposed += OnDisposed;
                await _alarmHandler.Handle(long.Parse(scheduleId), isImmediate);
            }
            catch (Exception e)
            {
                Logger.Error(e, "An error happened when creating the task to ring the alarm.");
                Dispose();
            }
            finally
            {
                Lock.Release();
                pendingIntent.Finish();
            }
        }

        private void OnDisposed(object sender, bool e)
        {
            Dispose(true);
        }

        private bool _disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (_alarmHandler != null)
            {
                _alarmHandler.Disposed -= OnDisposed;
            }

            _context?.StopService(_intent);
            BootstrapHelper.Remove(_context);

            AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException -= UnobserverdTaskException;

            _disposed = true;

            base.Dispose(disposing);
        }
    }
}