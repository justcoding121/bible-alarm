#nullable enable

using System;
using System.Collections.Generic;
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
    /// Gets a safe savedInstanceState by removing fragment state that could cause crashes.
    /// </summary>
    public static Bundle? GetSafeSavedInstanceState(Bundle? savedInstanceState)
    {
        if (savedInstanceState == null)
        {
            return null;
        }

        try
        {
            if (HasFragmentState(savedInstanceState))
            {
                logger.Information("Detected fragment state in savedInstanceState - ignoring to prevent NavigationRootManager crash");
                return null;
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error checking fragment state - ignoring savedInstanceState to be safe");
            return null;
        }

        return savedInstanceState;
    }

    /// <summary>
    /// Checks if the savedInstanceState contains fragment state keys.
    /// </summary>
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

    /// <summary>
    /// Checks if a key is a fragment state key.
    /// </summary>
    private static bool IsFragmentStateKey(string? key)
    {
        if (key == null)
        {
            return false;
        }

        return key.Contains("fragment") || key.Contains("Fragment") ||
               key.Contains("androidx.lifecycle") || key.Contains("android:support");
    }

    /// <summary>
    /// Removes fragment state keys from the outState bundle to prevent crashes.
    /// </summary>
    public static void RemoveFragmentStateKeys(Bundle? outState)
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

        logger.Information("Prevented saving {Count} fragment state keys to avoid NavigationRootManager crash", keysToRemove.Count);
    }

    /// <summary>
    /// Gets a list of fragment state keys from the bundle.
    /// </summary>
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

    /// <summary>
    /// Checks if an IllegalArgumentException is a fragment restoration error.
    /// </summary>
    public static bool IsFragmentRestorationError(IllegalArgumentException ex)
    {
        return ex.Message?.Contains("No view found for id") == true &&
               ex.Message?.Contains("legacy") == true;
    }

    /// <summary>
    /// Handles fragment restoration errors by restarting the activity.
    /// </summary>
    public static void HandleFragmentRestorationError(Activity activity, IllegalArgumentException ex)
    {
        logger.Warning(ex, "Fragment restoration failed due to stale state - restarting activity for clean state");

        // Create a completely fresh intent to avoid any stale state
        // Use Handler to post the restart to ensure it happens after the current lifecycle method completes
        // This prevents the black screen by ensuring proper activity transition
        try
        {
            var handler = new Handler(Looper.MainLooper);
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
