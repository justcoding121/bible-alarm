#nullable enable

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Platforms.Android.Services.Helpers;
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
            PerformOnCreateInitialization(savedInstanceState);
            SetupActivityComponents();
            Logger.Debug("MainActivity: OnCreate completed successfully");
        }
        catch (Exception ex)
        {
            MainActivityExceptionHandler.HandleOnCreateException(ex);
            throw;
        }
    }

    private void PerformOnCreateInitialization(Bundle? savedInstanceState)
    {
        InitializeMauiApp();
        var safeSavedInstanceState = MainActivityFragmentStateHelper.GetSafeSavedInstanceState(savedInstanceState);

        Logger.Debug("MainActivity: Calling base.OnCreate()");
        base.OnCreate(safeSavedInstanceState);
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

    protected override void OnStart()
    {
        MainActivityLifecycleHelper.OnStart(this, () => base.OnStart());
    }

    protected override void OnResume()
    {
        base.OnResume();
        backgroundTaskHelper.UpdateResumeTime();
    }

    protected override void OnPause()
    {
        base.OnPause();
        backgroundTaskHelper.ClearResumeTime();
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

}
