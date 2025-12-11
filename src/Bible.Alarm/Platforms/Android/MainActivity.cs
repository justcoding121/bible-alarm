using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Platforms.Android.Services.AndroidServices;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Polly;
using Polly.Retry;
using Serilog;
using System.Threading;
using System.Threading.Tasks;

namespace Bible.Alarm.Platforms.Android;

[Activity(Label = "Bible Alarm", Theme = "@style/MainTheme", LaunchMode = LaunchMode.SingleTop, MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private static readonly ILogger Logger = Log.ForContext<MainActivity>();
    private IAndroidAlarmHandler _alarmHandler;
    private DateTime? _lastResumeTime;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        // IMPORTANT: Create MAUI app BEFORE calling base.OnCreate()
        // This ensures the service provider is available when MAUI's lifecycle events try to access it
        // If the app was previously disposed (swiped out), CreateAndStore() will create a new app instance
        MauiAppHolder.CreateAndStore();

        base.OnCreate(savedInstanceState);

        // Set up global exception handlers
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;

        // NOTE: Do NOT call InitializePlatformBootstrap here for foreground launches
        // WindowSetupService.CreateWindow() will handle bootstrap and send InitializedMessage
        // Bootstrap is only needed here for background services/jobs, not for foreground UI launches

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
        Logger.Error(e.ExceptionObject as Exception, "Unhandled exception. IsTerminating: {IsTerminating}", 
            e.IsTerminating);
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
                    
                    // Wait for bootstrap to complete before using database services
                    await MauiProgram.WaitForBootstrapAsync();

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
        Task.Run(() =>
        {
            try
            {
                if (_lastResumeTime.HasValue && DateTime.Now.Subtract(_lastResumeTime.Value).TotalSeconds >= 3)
                {
                    var intent = new Intent(this, typeof(AlarmSetupService));
                    intent.PutExtra("Action", "SetupBackgroundTasks");
                    StartService(intent);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error setting up background tasks");
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

    protected override void OnDestroy()
    {
        base.OnDestroy();

        // Call player dismiss action when activity is destroyed (e.g., user swipes out the app)
        // This is NOT called when app is just backgrounded (OnPause handles that)
        // IMPORTANT: Wait for playback to stop before disposing MauiApp to prevent ObjectDisposedException
        try
        {
            // Check if MauiAppHolder is still initialized before trying to use it
            if (MauiAppHolder.IsInitialized)
            {
                var serviceProvider = MauiAppHolder.Services;
                if (serviceProvider != null)
                {
                    var windowSetupService = serviceProvider.GetService<IWindowSetupService>();
                    windowSetupService.TearDown();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error in MainActivity.OnDestroy while attempting to dismiss player");
        }

     
    }

}