#nullable enable

using Android.App;
using Android.OS;
using Bible.Alarm.Common;
using Bible.Alarm.Services.UI.Interfaces;
using Java.Lang;
using Serilog;
using Exception = System.Exception;

namespace Bible.Alarm.Platforms.Android.Services.Helpers;

/// <summary>
/// Handles lifecycle management for MainActivity.
/// </summary>
public static class MainActivityLifecycleHelper
{
    private static readonly ILogger logger = Log.ForContext(typeof(MainActivityLifecycleHelper));

    /// <summary>
    /// Handles OnStart lifecycle event with fragment restoration error handling.
    /// </summary>
    public static void OnStart(Activity activity, Action baseOnStart)
    {
        try
        {
            baseOnStart();
        }
        catch (IllegalArgumentException ex) when (MainActivityFragmentStateHelper.IsFragmentRestorationError(ex))
        {
            MainActivityFragmentStateHelper.HandleFragmentRestorationError(activity, ex);
        }
    }

    /// <summary>
    /// Handles OnResume lifecycle event with fragment restoration error handling.
    /// </summary>
    public static void OnResume(Activity activity, Action baseOnResume)
    {
        try
        {
            baseOnResume();
        }
        catch (IllegalArgumentException ex) when (MainActivityFragmentStateHelper.IsFragmentRestorationError(ex))
        {
            MainActivityFragmentStateHelper.HandleFragmentRestorationError(activity, ex);
        }
        catch (Java.Lang.RuntimeException runtimeEx) when (MainActivityFragmentStateHelper.IsFragmentRestorationError(runtimeEx))
        {
            MainActivityFragmentStateHelper.HandleFragmentRestorationError(activity, runtimeEx);
        }
    }

    /// <summary>
    /// Handles OnSaveInstanceState with safe state saving.
    /// </summary>
    public static void OnSaveInstanceState(Activity activity, Bundle outState, Action<Bundle> baseOnSaveInstanceState)
    {
        try
        {
            SaveInstanceStateSafely(outState, baseOnSaveInstanceState);
        }
        catch (Exception ex)
        {
            HandleSaveInstanceStateError(outState, ex, baseOnSaveInstanceState);
        }
    }

    /// <summary>
    /// Saves instance state safely by clearing all state to prevent fragment restoration crashes.
    /// </summary>
    private static void SaveInstanceStateSafely(Bundle outState, Action<Bundle> baseOnSaveInstanceState)
    {
        // Call base first to allow normal save, then clear everything.
        // This prevents fragment state from being saved, which could cause
        // NavigationRootManager crashes when the app is restored.
        baseOnSaveInstanceState(outState);
        MainActivityFragmentStateHelper.ClearAllState(outState);
    }

    /// <summary>
    /// Handles errors during instance state saving.
    /// </summary>
    private static void HandleSaveInstanceStateError(Bundle outState, Exception ex, Action<Bundle> baseOnSaveInstanceState)
    {
        logger.Warning(ex, "Error in OnSaveInstanceState - proceeding anyway");
        TrySaveBaseState(outState, baseOnSaveInstanceState);
    }

    /// <summary>
    /// Attempts to save base state as a fallback.
    /// </summary>
    private static void TrySaveBaseState(Bundle outState, Action<Bundle> baseOnSaveInstanceState)
    {
        try
        {
            baseOnSaveInstanceState(outState);
        }
        catch
        {
            // Ignore errors in base call
        }
    }

    /// <summary>
    /// Performs activity teardown when the activity is destroyed.
    /// </summary>
    public static void PerformActivityTeardown()
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
            logger.Error(ex, "Error in MainActivity.OnDestroy while attempting to dismiss player");
        }
    }

    /// <summary>
    /// Tears down the WindowSetupService.
    /// </summary>
    private static void TearDownWindowSetupService()
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
