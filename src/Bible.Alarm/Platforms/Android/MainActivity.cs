using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Platforms.Android.Services.AndroidServices;
using Serilog;

namespace Bible.Alarm.Platforms.Android;

[Activity(Theme = "@style/MainTheme", LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private static readonly ILogger Logger = Log.ForContext<MainActivity>();
    private IAndroidAlarmHandler _alarmHandler;
    private DateTime? _lastResumeTime;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Set up global exception handlers
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;

        // BootstrapHelper is already initialized in SplashActivity
        // No need to call it again here for foreground scenarios

        // Handle incoming intents (e.g., from notifications)
        HandleIncomingIntent();

        // Set up background tasks
        SetupBackgroundTasks();
    }

    private void UnobservedTaskExceptionHandler(object sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Error(e.Exception, "Unobserved task exception.");
    }

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        Logger.Error("Unhandled exception.", e.SerializeObject());
    }

    private void HandleIncomingIntent()
    {
        if (Intent?.Extras == null) return;
        var scheduleId = Intent.Extras.GetInt("schedule_id", int.MinValue);
        if (scheduleId != int.MinValue)
        {
            Task.Run(async () =>
            {
                try
                {
                    // Ensure MauiApp is created exactly once (thread-safe)
                    // This handles incoming intents (e.g., from notifications)
                    MauiAppHolder.CreateAndStore();
                    // Run bootstrapper after CreateAndStore for background launch
                    MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                    
                    _alarmHandler ??= ServiceProviderManager.GetService<IAndroidAlarmHandler>();
                    await _alarmHandler.HandleAsync(scheduleId, false);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Error handling incoming alarm intent");
                }
            });
        }
    }

    private void SetupBackgroundTasks()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    if (_lastResumeTime.HasValue && DateTime.Now.Subtract(_lastResumeTime.Value).TotalSeconds >= 3)
                    {
                        var intent = new Intent(this, typeof(AlarmSetupService));
                        intent.PutExtra("Action", "SetupBackgroundTasks");
                        StartService(intent);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error setting up background tasks");
                    break;
                }
                await Task.Delay(1000);
            }
        });
    }

    protected override void OnResume()
    {
        base.OnResume();
        _lastResumeTime = DateTime.Now;
    }

    protected override void OnPause()
    {
        base.OnPause();
        _lastResumeTime = null;
    }
}