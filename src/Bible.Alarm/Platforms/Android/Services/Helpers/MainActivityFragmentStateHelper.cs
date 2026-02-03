#nullable enable

using Android.App;
using Android.Content;
using Android.OS;
using Java.Lang;
using Serilog;
using Exception = System.Exception;

namespace Bible.Alarm.Platforms.Android.Services.Helpers;

/// <summary>
/// Handles fragment state management for MainActivity to prevent NavigationRootManager crashes.
/// </summary>
public static class MainActivityFragmentStateHelper
{
    private static readonly ILogger logger = Log.ForContext(typeof(MainActivityFragmentStateHelper));

    /// <summary>
    /// Gets a safe savedInstanceState by completely ignoring any saved state on fresh launch.
    /// MAUI apps should always start with a clean slate to avoid NavigationRootManager crashes
    /// caused by stale fragment state that references views that don't exist yet.
    /// </summary>
    public static Bundle? GetSafeSavedInstanceState(Bundle? savedInstanceState)
    {
        if (savedInstanceState == null)
        {
            return null;
        }

        // ALWAYS return null for MAUI apps to prevent fragment restoration crashes.
        // The NavigationRootManager_ElementBasedFragment crash occurs when Android's
        // FragmentManager tries to restore a fragment to a view ID ("legacy") that
        // doesn't exist because MAUI hasn't set up the view hierarchy yet.
        //
        // This is the ROOT CAUSE fix: by returning null, we tell Android not to
        // restore any state, which prevents the fragment manager from attempting
        // to restore fragments to non-existent views.
        logger.Information("Ignoring savedInstanceState to prevent NavigationRootManager fragment restoration crash");
        return null;
    }

    /// <summary>
    /// Clears all state from the outState bundle to prevent fragment restoration crashes.
    /// MAUI apps should not rely on Android's instance state restoration.
    /// </summary>
    public static void ClearAllState(Bundle? outState)
    {
        if (outState == null)
        {
            return;
        }

        try
        {
            outState.Clear();
            logger.Debug("Cleared all saved instance state to prevent fragment restoration crashes");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to clear saved instance state");
        }
    }

    /// <summary>
    /// Checks if an IllegalArgumentException is a fragment restoration error.
    /// </summary>
    public static bool IsFragmentRestorationError(IllegalArgumentException ex)
    {
        return ex.Message?.Contains("No view found for id") == true &&
               ex.Message?.Contains("legacy") == true;
    }

    /// <summary>
    /// Checks if a RuntimeException is a fragment restoration error (may wrap IllegalArgumentException).
    /// </summary>
    public static bool IsFragmentRestorationError(Java.Lang.RuntimeException ex)
    {
        if (ex.Message?.Contains("No view found for id") == true &&
            ex.Message?.Contains("legacy") == true)
        {
            return true;
        }

        if (ex.InnerException is IllegalArgumentException innerEx)
        {
            return IsFragmentRestorationError(innerEx);
        }

        return false;
    }

    /// <summary>
    /// Handles fragment restoration errors by restarting the activity.
    /// </summary>
    public static void HandleFragmentRestorationError(Activity activity, IllegalArgumentException ex)
    {
        HandleFragmentRestorationErrorInternal(activity, ex);
    }

    /// <summary>
    /// Handles fragment restoration errors by restarting the activity (for RuntimeException).
    /// </summary>
    public static void HandleFragmentRestorationError(Activity activity, Java.Lang.RuntimeException ex)
    {
        var actualException = ex.InnerException as IllegalArgumentException ?? 
                             new IllegalArgumentException(ex.Message ?? "Fragment restoration error");
        HandleFragmentRestorationErrorInternal(activity, actualException);
    }

    /// <summary>
    /// Internal method to handle fragment restoration errors by restarting the activity.
    /// </summary>
    private static void HandleFragmentRestorationErrorInternal(Activity activity, IllegalArgumentException ex)
    {
        logger.Warning(ex, "Fragment restoration failed due to stale state - restarting activity for clean state");

        // Create a completely fresh intent to avoid any stale state
        // Use Handler to post the restart to ensure it happens after the current lifecycle method completes
        // This prevents the black screen by ensuring proper activity transition
        try
        {
            var handler = new Handler(Looper.MainLooper ?? throw new InvalidOperationException("MainLooper cannot be null"));
            handler.Post(() =>
            {
                try
                {
                    // Create a fresh intent for the main launcher activity
                    var intent = new Intent(activity, activity.GetType());
                    intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.NewTask | ActivityFlags.ClearTask);

                    // Remove any potential fragment state extras
                    intent.RemoveExtra("android:support:fragments");
                    intent.RemoveExtra("androidx.lifecycle");

                    // Start the new activity
                    activity.StartActivity(intent);

                    // Use FinishAffinity to properly close this activity and any related activities
                    // This ensures a clean transition without black screen
                    activity.FinishAffinity();

                    logger.Information("Activity restarted to recover from fragment restoration error");
                }
                catch (Exception restartEx)
                {
                    logger.Error(restartEx, "Failed to restart activity in handler - app may need to be manually restarted");
                    // Fallback: try to finish this activity to allow system to recover
                    try
                    {
                        activity.Finish();
                    }
                    catch
                    {
                        // Ignore errors during fallback
                    }
                }
            });
        }
        catch (Exception handlerEx)
        {
            logger.Error(handlerEx, "Failed to post restart handler - attempting immediate restart");
            // Fallback: try immediate restart
            try
            {
                var intent = new Intent(activity, activity.GetType());
                intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.NewTask | ActivityFlags.ClearTask);
                activity.StartActivity(intent);
                activity.Finish();
            }
            catch (Exception fallbackEx)
            {
                logger.Error(fallbackEx, "All restart attempts failed - app may need to be manually restarted");
            }
        }
    }
}
