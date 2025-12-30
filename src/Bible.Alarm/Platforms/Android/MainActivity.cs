#nullable enable

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Platforms.Android.Services.AndroidServices;
using Bible.Alarm.Services.UI.Interfaces;
using Java.Lang;
using Serilog;
using Exception = System.Exception;

namespace Bible.Alarm.Platforms.Android;

[Activity(Label = "Bible Alarm", Theme = "@style/MainTheme", LaunchMode = LaunchMode.SingleTop, MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // Lazy logger initialization to ensure Serilog is configured first
    private static ILogger? logger;
    private static ILogger Logger => logger ??= Serilog.Log.ForContext<MainActivity>();

    private IAndroidAlarmHandler? alarmHandler;
    private DateTime? lastResumeTime;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Note: MediaSession is already created in MainApplication.OnCreate() which runs before this
        // No need to create it here since MainApplication always runs first on process start

#if DEBUG
        Logger.Information("[BOOTSTRAP] App launch started - Stopwatch started at {ElapsedMs}ms", BootstrapTimingHelper.GetElapsedMilliseconds());
#endif
        SetupGlobalExceptionHandlers();
        Logger.Debug("MainActivity: OnCreate started");

        try
        {
            PerformOnCreateInitialization(savedInstanceState);
            SetupActivityComponents();
            Logger.Debug("MainActivity: OnCreate completed successfully");
        }
        catch (Exception ex)
        {
            HandleOnCreateException(ex);
            throw;
        }
    }

    private void PerformOnCreateInitialization(Bundle? savedInstanceState)
    {
        InitializeMauiApp();
        var safeSavedInstanceState = GetSafeSavedInstanceState(savedInstanceState);

        Logger.Debug("MainActivity: Calling base.OnCreate()");
        base.OnCreate(safeSavedInstanceState);
        Logger.Debug("MainActivity: base.OnCreate() completed");
    }

    private void SetupGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;
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

    private Bundle? GetSafeSavedInstanceState(Bundle? savedInstanceState)
    {
        if (savedInstanceState == null)
        {
            return null;
        }

        try
        {
            if (HasFragmentState(savedInstanceState))
            {
                Logger.Information("Detected fragment state in savedInstanceState - ignoring to prevent NavigationRootManager crash");
                return null;
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error checking fragment state - ignoring savedInstanceState to be safe");
            return null;
        }

        return savedInstanceState;
    }

    private static bool HasFragmentState(Bundle savedInstanceState)
    {
        var keySet = savedInstanceState.KeySet();
        if (keySet == null)
        {
            return false;
        }

        foreach (var key in keySet)
        {
            if (IsFragmentStateKey(key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFragmentStateKey(string? key)
    {
        if (key == null)
        {
            return false;
        }

        return key.Contains("fragment") || key.Contains("Fragment") ||
               key.Contains("androidx.lifecycle") || key.Contains("android:support");
    }


    private void SetupActivityComponents()
    {
        Logger.Debug("MainActivity: Setting up intents and background tasks");
        HandleIncomingIntent();
        SetupBackgroundTasks();
    }

    private void HandleOnCreateException(Exception ex)
    {
        Logger.Fatal(ex, "FATAL ERROR in OnCreate: {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);

        if (ex.InnerException != null)
        {
            Logger.Fatal(ex.InnerException, "Inner exception in OnCreate: {ExceptionType}: {Message}", ex.InnerException.GetType().Name, ex.InnerException.Message);
        }

        try
        {
            Logger.Fatal(ex, "Fatal error in MainActivity.OnCreate - app will crash");
        }
        catch
        {
            // Serilog not available, already logged with AndroidLog
        }
    }

    private void UnobservedTaskExceptionHandler(object? sender, UnobservedTaskExceptionEventArgs e) => Logger.Error(e.Exception, "Unobserved task exception.");

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        Logger.Error(e.ExceptionObject as Exception, "Unhandled exception. IsTerminating: {IsTerminating}",
            e.IsTerminating);
    }

    private void HandleIncomingIntent()
    {
        if (!HasValidScheduleIdFromIntent())
        {
            return;
        }

        var extras = Intent?.Extras;
        if (extras == null)
        {
            return;
        }

        var scheduleId = extras.GetInt("schedule_id", int.MinValue);
        Task.Run(() => HandleAlarmIntentAsync(scheduleId));
    }

    private bool HasValidScheduleIdFromIntent()
    {
        return Intent?.Extras != null &&
               Intent.Extras.GetInt("schedule_id", int.MinValue) != int.MinValue;
    }

    private async Task HandleAlarmIntentAsync(int scheduleId)
    {
        try
        {
            await InitializeAppForBackgroundLaunchAsync();
            await HandleAlarmAsync(scheduleId);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error handling incoming alarm intent");
        }
    }

    private async Task InitializeAppForBackgroundLaunchAsync()
    {
        // Ensure MauiApp is created exactly once (thread-safe)
        // This handles incoming intents (e.g., from notifications)
        MauiAppHolder.CreateAndStore();
        // Run bootstrapper after CreateAndStore for background launch
        MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
        // Wait for bootstrap to complete before using database services
        await MauiProgram.WaitForBootstrapAsync();
    }

    private async Task HandleAlarmAsync(int scheduleId)
    {
        alarmHandler ??= ServiceProviderManager.GetService<IAndroidAlarmHandler>();
        await alarmHandler.HandleAsync(scheduleId, false);
    }

    private void SetupBackgroundTasks()
    {
        Task.Run(() =>
        {
            try
            {
                if (ShouldSetupBackgroundTasks())
                {
                    StartAlarmSetupService();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error setting up background tasks");
            }
        });
    }

    private bool ShouldSetupBackgroundTasks()
    {
        return lastResumeTime.HasValue &&
               DateTime.Now.Subtract(lastResumeTime.Value).TotalSeconds >= 3;
    }

    private void StartAlarmSetupService()
    {
        var intent = new Intent(this, typeof(AlarmSetupService));
        intent.PutExtra("Action", "SetupBackgroundTasks");
        StartService(intent);
    }

    protected override void OnStart()
    {
        try
        {
            base.OnStart();
        }
        catch (IllegalArgumentException ex) when (IsFragmentRestorationError(ex))
        {
            HandleFragmentRestorationError(ex);
        }
    }

    private static bool IsFragmentRestorationError(IllegalArgumentException ex)
    {
        return ex.Message?.Contains("No view found for id") == true &&
               ex.Message?.Contains("legacy") == true;
    }

    private void HandleFragmentRestorationError(IllegalArgumentException ex)
    {
        Logger.Warning(ex, "Fragment restoration failed due to stale state - clearing fragments and retrying");

        try
        {
            ClearAllFragments();
            base.OnStart();
        }
        catch (Exception retryEx)
        {
            Logger.Error(retryEx, "Failed to recover from fragment restoration error - app may be in inconsistent state");
            throw;
        }
    }

    private void ClearAllFragments()
    {
        var fragmentManager = SupportFragmentManager;
        if (fragmentManager == null)
        {
            return;
        }

        var fragments = fragmentManager.Fragments;
        if (fragments == null || fragments.Count == 0)
        {
            return;
        }

        var transaction = fragmentManager.BeginTransaction();
        foreach (var fragment in fragments)
        {
            if (fragment != null)
            {
                transaction.Remove(fragment);
            }
        }
        transaction.CommitAllowingStateLoss();
        fragmentManager.ExecutePendingTransactions();
    }

    protected override void OnResume()
    {
        base.OnResume();
        lastResumeTime = DateTime.Now;
    }

    protected override void OnPause()
    {
        base.OnPause();
        lastResumeTime = null;
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        try
        {
            SaveInstanceStateSafely(outState);
        }
        catch (Exception ex)
        {
            HandleSaveInstanceStateError(outState, ex);
        }
    }

    private void SaveInstanceStateSafely(Bundle outState)
    {
        base.OnSaveInstanceState(outState);
        RemoveFragmentStateKeys(outState);
    }

    private void HandleSaveInstanceStateError(Bundle outState, Exception ex)
    {
        Logger.Warning(ex, "Error in OnSaveInstanceState - proceeding anyway");
        TrySaveBaseState(outState);
    }

    private void RemoveFragmentStateKeys(Bundle? outState)
    {
        if (outState == null)
        {
            return;
        }

        var keysToRemove = GetFragmentStateKeys(outState);
        if (keysToRemove.Count == 0)
        {
            return;
        }

        foreach (var key in keysToRemove)
        {
            outState.Remove(key);
        }

        Logger.Information("Prevented saving {Count} fragment state keys to avoid NavigationRootManager crash", keysToRemove.Count);
    }

    private static List<string> GetFragmentStateKeys(Bundle outState)
    {
        var keysToRemove = new List<string>();
        var keySet = outState.KeySet();

        if (keySet == null)
        {
            return keysToRemove;
        }

        foreach (var key in keySet)
        {
            if (IsFragmentStateKey(key))
            {
                keysToRemove.Add(key);
            }
        }

        return keysToRemove;
    }

    private void TrySaveBaseState(Bundle outState)
    {
        try
        {
            base.OnSaveInstanceState(outState);
        }
        catch
        {
            // Ignore errors in base call
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        PerformActivityTeardown();
    }

    private void PerformActivityTeardown()
    {
        // Call player dismiss action when activity is destroyed (e.g., user swipes out the app)
        // This is NOT called when app is just backgrounded (OnPause handles that)
        // IMPORTANT: Wait for playback to stop before disposing MauiApp to prevent ObjectDisposedException
        try
        {
            TearDownWindowSetupService();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error in MainActivity.OnDestroy while attempting to dismiss player");
        }
    }

    private void TearDownWindowSetupService()
    {
        // Check if MauiAppHolder is still initialized before trying to use it
        if (MauiAppHolder.IsInitialized)
        {
            var serviceProvider = MauiAppHolder.Services;
            if (serviceProvider != null)
            {
                var windowSetupService = serviceProvider.GetService<IWindowSetupService>();
                windowSetupService?.TearDown();
            }
        }
    }

}
