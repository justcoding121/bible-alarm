#nullable enable

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Bible.Alarm.Common;
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
    private static readonly ILogger logger = Log.ForContext<MainActivity>();
    private IAndroidAlarmHandler? alarmHandler;
    private DateTime? lastResumeTime;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // IMPORTANT: Create MAUI app BEFORE calling base.OnCreate()
        // This ensures the service provider is available when MAUI's lifecycle events try to access it
        // If the app was previously disposed (swiped out), CreateAndStore() will create a new app instance
        MauiAppHolder.CreateAndStore();

        // Fix for MAUI NavigationRootManager fragment state restoration issue
        // If savedInstanceState contains stale fragment state that references non-existent views (like id/legacy),
        // pass null to prevent fragment restoration. This can happen after app updates or when MAUI framework
        // tries to restore fragments with view IDs that no longer exist.
        Bundle? safeSavedInstanceState = savedInstanceState;
        if (savedInstanceState != null)
        {
            try
            {
                // Check if savedInstanceState contains fragment state that might be stale
                var hasFragmentState = false;
                foreach (var key in savedInstanceState?.KeySet() ?? [])
                {
                    if (key != null && (key.Contains("fragment") || key.Contains("Fragment") ||
                        key.Contains("androidx.lifecycle") || key.Contains("android:support")))
                    {
                        hasFragmentState = true;
                        break;
                    }
                }

                if (hasFragmentState)
                {
                    logger.Information("Detected fragment state in savedInstanceState - ignoring to prevent NavigationRootManager crash");
                    // Pass null to prevent fragment restoration - MAUI will recreate navigation from scratch
                    safeSavedInstanceState = null;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error checking fragment state - ignoring savedInstanceState to be safe");
                safeSavedInstanceState = null;
            }
        }

        base.OnCreate(safeSavedInstanceState);

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

    private void UnobservedTaskExceptionHandler(object? sender, UnobservedTaskExceptionEventArgs e) => logger.Error(e.Exception, "Unobserved task exception.");

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        logger.Error(e.ExceptionObject as Exception, "Unhandled exception. IsTerminating: {IsTerminating}",
            e.IsTerminating);
    }

    private void HandleIncomingIntent()
    {
        if (Intent?.Extras == null)
        {
            return;
        }

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

                    alarmHandler ??= ServiceProviderManager.GetService<IAndroidAlarmHandler>();
                    await alarmHandler.HandleAsync(scheduleId, false);
                }
                catch (Exception e)
                {
                    logger.Error(e, "Error handling incoming alarm intent");
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
                if (lastResumeTime.HasValue && DateTime.Now.Subtract(lastResumeTime.Value).TotalSeconds >= 3)
                {
                    var intent = new Intent(this, typeof(AlarmSetupService));
                    intent.PutExtra("Action", "SetupBackgroundTasks");
                    StartService(intent);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting up background tasks");
            }
        });
    }

    protected override void OnStart()
    {
        // Fix for MAUI NavigationRootManager fragment state restoration issue
        // Fragment restoration happens in OnStart(), so we need to handle stale fragment restoration
        // that references non-existent views (like id/legacy)
        try
        {
            base.OnStart();
        }
        catch (IllegalArgumentException ex) when (ex.Message?.Contains("No view found for id") == true && ex.Message?.Contains("legacy") == true)
        {
            // Fragment restoration failed due to stale state - clear fragments and retry
            logger.Warning(ex, "Fragment restoration failed due to stale state - clearing fragments and retrying");
            try
            {
                var fragmentManager = SupportFragmentManager;
                if (fragmentManager != null)
                {
                    // Clear all fragments
                    var fragments = fragmentManager.Fragments;
                    if (fragments != null && fragments.Count > 0)
                    {
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
                }

                // Retry base.OnStart() after clearing fragments
                base.OnStart();
            }
            catch (Exception retryEx)
            {
                logger.Error(retryEx, "Failed to recover from fragment restoration error - app may be in inconsistent state");
                // Re-throw to let Android handle it
                throw;
            }
        }
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
        // Fix for MAUI NavigationRootManager fragment state restoration issue
        // Prevent saving fragment state to avoid stale fragment restoration crashes
        // MAUI will recreate the navigation structure from scratch on next launch
        // This prevents crashes when fragment state references non-existent views (like id/legacy)
        try
        {
            // Call base to save other state (like activity state)
            base.OnSaveInstanceState(outState);

            // Remove fragment state keys to prevent stale fragment restoration
            if (outState != null)
            {
                var keysToRemove = new List<string>();
                var keySet = outState.KeySet();
                if (keySet != null)
                {
                    foreach (var key in keySet)
                    {
                        if (key != null && (key.Contains("fragment") || key.Contains("Fragment") ||
                            key.Contains("androidx.lifecycle") || key.Contains("android:support")))
                        {
                            keysToRemove.Add(key);
                        }
                    }
                }

                if (keysToRemove.Count > 0)
                {
                    foreach (var key in keysToRemove)
                    {
                        outState.Remove(key);
                    }

                    logger.Information("Prevented saving {Count} fragment state keys to avoid NavigationRootManager crash", keysToRemove.Count);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error in OnSaveInstanceState - proceeding anyway");
            // Still call base to ensure other state is saved
            try
            {
                base.OnSaveInstanceState(outState);
            }
            catch
            {
                // Ignore errors in base call
            }
        }
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
                    windowSetupService?.TearDown();
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error in MainActivity.OnDestroy while attempting to dismiss player");
        }


    }

}
