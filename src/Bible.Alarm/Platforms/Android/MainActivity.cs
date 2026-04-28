#nullable enable

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Platforms.Android.Services.Helpers;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Serilog;
using Exception = System.Exception;

namespace Bible.Alarm.Platforms.Android;

[Activity(Label = "Bible Alarm", Theme = "@style/MainTheme", LaunchMode = LaunchMode.SingleTop, MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // Lazy logger initialization to ensure Serilog is configured first
    private static ILogger? logger;
    private static ILogger Logger => logger ??= Serilog.Log.ForContext<MainActivity>();

    private MainActivityIntentHandler? intentHandler;
    private MainActivityBackgroundTaskHelper? backgroundTaskHelper;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Note: MediaSession is already created in MainApplication.OnCreate() which runs before this
        // No need to create it here since MainApplication always runs first on process start

#if DEBUG
        Logger.Information("[BOOTSTRAP] App launch started - Stopwatch started at {ElapsedMs}ms", BootstrapTimingHelper.GetElapsedMilliseconds());
#endif

        // Initialize helpers
        intentHandler = new MainActivityIntentHandler(Logger);
        backgroundTaskHelper = new MainActivityBackgroundTaskHelper(Logger, this);

        MainActivityExceptionHandler.SetupGlobalExceptionHandlers();
        Logger.Debug("MainActivity: OnCreate started");

        try
        {
            PerformOnCreateInitialization();
            SetupActivityComponents();
            Logger.Debug("MainActivity: OnCreate completed successfully");
        }
        catch (Exception ex)
        {
            MainActivityExceptionHandler.HandleOnCreateException(ex);
            throw;
        }
    }

    private void PerformOnCreateInitialization()
    {
        InitializeMauiApp();

        // Always pass null to prevent Android from restoring stale fragment state.
        // This app rebuilds all state from databases/Fluxor during bootstrap,
        // so Android's saved instance state is never needed.
        // Lifecycle guards in OnStart/OnResume/OnPostResume catch any real fragment errors.
        Logger.Debug("MainActivity: Calling base.OnCreate()");
        base.OnCreate(null);
        Logger.Debug("MainActivity: base.OnCreate() completed");
    }

    private static void InitializeMauiApp()
    {
#if DEBUG
        var diStartTime = BootstrapTimingHelper.GetElapsedMilliseconds();
        Logger.Information("[BOOTSTRAP] DI setup starting at {ElapsedMs}ms", diStartTime);
#endif

        Logger.Debug("MainActivity: Calling MauiAppHolder.CreateAndStore()");
        MauiAppHolder.CreateAndStore();

#if DEBUG
        var diElapsed = BootstrapTimingHelper.GetElapsedMilliseconds() - diStartTime;
        Logger.Information("[BOOTSTRAP] DI setup completed in {ElapsedMs}ms (total: {TotalMs}ms)", diElapsed, BootstrapTimingHelper.GetElapsedMilliseconds());
#endif
        Logger.Debug("MainActivity: MauiAppHolder.CreateAndStore() completed");
    }

    private void SetupActivityComponents()
    {
        Logger.Debug("MainActivity: Setting up intents and background tasks");
        intentHandler?.HandleIncomingIntent(Intent);
        backgroundTaskHelper?.SetupBackgroundTasks();
    }

    /// <summary>
    /// Handles new intents when activity is already running (SingleTop mode).
    /// Called when user taps notification while activity is already in foreground.
    /// </summary>
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        // Note: base.OnNewIntent should handle setting the intent for getIntent()
        // If needed, we can use reflection for newer API levels that require ComponentCaller
        Logger.Debug("MainActivity: OnNewIntent called - handling notification tap");
        intentHandler?.HandleIncomingIntent(intent);
    }

    protected override void OnStart()
    {
        MainActivityLifecycleHelper.OnStart(this, () => base.OnStart());
    }

    protected override void OnResume()
    {
        MainActivityLifecycleHelper.OnResume(this, () =>
        {
            base.OnResume();
            backgroundTaskHelper?.UpdateResumeTime();
        });
    }

    protected override void OnPostResume()
    {
        MainActivityLifecycleHelper.OnPostResume(this, () => base.OnPostResume());
    }

    protected override void OnPause()
    {
        base.OnPause();
        backgroundTaskHelper?.ClearResumeTime();
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        MainActivityLifecycleHelper.OnSaveInstanceState(this, outState, base.OnSaveInstanceState);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        MainActivityLifecycleHelper.PerformActivityTeardown();
    }

    /// <summary>
    /// Handles permission request results from Android system.
    /// Routes notification permission results to NotificationPermissionService.
    /// </summary>
    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        
        if (requestCode == NotificationPermissionService.NotificationPermissionRequestCode)
        {
            Logger.Debug("MainActivity: Received notification permission result");
            NotificationPermissionService.Instance.HandlePermissionResult(requestCode, permissions, grantResults);

            if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
            {
                RescheduleAllAlarmsInBackground();
            }
        }
    }

    private static void RescheduleAllAlarmsInBackground()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (!BootstrapHelper.IsBootstrapCompleted())
                {
                    await BootstrapHelper.WaitForBootstrapAsync();
                }

                var schedulerService = ServiceProviderManager.GetService<ISchedulerService>();
                if (schedulerService != null)
                {
                    Logger.Information("MainActivity: Notification permission granted - rescheduling all alarms");
                    await schedulerService.ProcessScheduledTasksAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "MainActivity: Failed to reschedule alarms after permission grant");
            }
        });
    }

}
