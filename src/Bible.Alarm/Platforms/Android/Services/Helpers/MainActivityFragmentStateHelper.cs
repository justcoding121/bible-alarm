#nullable enable

using System.Runtime.Versioning;
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
    /// Checks if MAUI's navigation fragments have valid view containers.
    /// Call this after base.OnCreate() to detect potential issues before OnStart() runs.
    /// Returns true if fragments appear healthy, false if there may be issues.
    /// </summary>
    public static bool ValidateFragmentViewContainers(Activity activity)
    {
        try
        {
            if (activity is not AndroidX.Fragment.App.FragmentActivity fragmentActivity)
            {
                return true;
            }

            var fragmentManager = fragmentActivity.SupportFragmentManager;

            // Force any pending fragment transactions to complete so they appear in Fragments.
            // MAUI's base.OnCreate() may add NavigationRootManager_ElementBasedFragment
            // via an asynchronous transaction that hasn't been committed yet.
            // Without this, fragmentManager.Fragments would be empty and we'd miss the issue.
            try
            {
                fragmentManager.ExecutePendingTransactions();
            }
            catch (Exception pendingEx)
            {
                // If executing pending transactions itself fails, the fragments are broken
                logger.Warning(pendingEx, "ExecutePendingTransactions failed - fragments are likely invalid");
                return false;
            }

            var fragments = fragmentManager.Fragments;

            if (fragments == null || fragments.Count == 0)
            {
                return true;
            }

            foreach (var fragment in fragments)
            {
                if (fragment == null)
                {
                    continue;
                }

                var containerId = fragment.Id;
                if (containerId == 0 || containerId == global::Android.Views.View.NoId)
                {
                    continue;
                }

                // Check if the container view exists
                var containerView = activity.FindViewById(containerId);
                if (containerView == null)
                {
                    logger.Warning(
                        "Fragment {FragmentType} references view ID 0x{ViewId:X} which doesn't exist",
                        fragment.GetType().Name,
                        containerId);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error validating fragment view containers");
            return true;
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
    /// Handles fragment restoration errors by killing the process and scheduling a restart.
    /// This is necessary because after a fragment error in OnStart, the lifecycle continues
    /// to OnResume and OnPostResume which will also fail, causing cascading crashes.
    /// The only reliable solution is to kill the process and start fresh.
    /// </summary>
    public static void HandleFragmentRestorationErrorWithProcessKill(Activity activity, IllegalArgumentException ex)
    {
        HandleFragmentRestorationErrorWithProcessKillInternal(activity, ex);
    }

    /// <summary>
    /// Handles fragment restoration errors by killing the process (for RuntimeException).
    /// </summary>
    public static void HandleFragmentRestorationErrorWithProcessKill(Activity activity, Java.Lang.RuntimeException ex)
    {
        var actualException = ex.InnerException as IllegalArgumentException ?? 
                             new IllegalArgumentException(ex.Message ?? "Fragment restoration error");
        HandleFragmentRestorationErrorWithProcessKillInternal(activity, actualException);
    }

    /// <summary>
    /// Internal method to handle fragment restoration errors by scheduling a restart
    /// via AlarmManager and then killing the process immediately.
    /// Limits consecutive restarts to prevent infinite crash loops.
    /// </summary>
    private static void HandleFragmentRestorationErrorWithProcessKillInternal(Activity activity, IllegalArgumentException ex)
    {
        logger.Warning(ex, "Fragment restoration failed - checking if restart is safe");

        if (IsInRestartLoop(activity))
        {
            logger.Warning("Fragment crash restart loop detected - NOT restarting to break the loop. "
                + "The app may need to be manually launched.");
            // Don't kill the process — let Android handle the lifecycle naturally.
            // The user can force-stop and reopen from the launcher.
            return;
        }

        RecordCrashTimestamp(activity);

        try
        {
            ScheduleAppRestart(activity);
        }
        catch (Exception scheduleEx)
        {
            logger.Error(scheduleEx, "Failed to schedule restart - process will be killed anyway");
        }

        // Kill the process immediately to prevent cascading lifecycle errors
        // The AlarmManager will restart the app with a clean slate
        logger.Information("Killing process to recover from fragment restoration error");
        Java.Lang.JavaSystem.Exit(0);
    }

    private const string CrashTimestampPrefKey = "fragment_crash_timestamp";
    private const string CrashCountPrefKey = "fragment_crash_count";
    private const int MaxConsecutiveRestarts = 2;

    // Crashes within this window are considered consecutive (part of a restart loop)
    private const long CrashWindowMs = 30_000;

    private static bool IsInRestartLoop(Activity activity)
    {
        try
        {
            var prefs = activity.GetSharedPreferences("fragment_crash", FileCreationMode.Private);
            if (prefs == null)
            {
                return false;
            }

            var lastCrashTime = prefs.GetLong(CrashTimestampPrefKey, 0);
            var crashCount = prefs.GetInt(CrashCountPrefKey, 0);
            var now = Java.Lang.JavaSystem.CurrentTimeMillis();

            // If the last crash was recent (within window), check counter
            if (now - lastCrashTime < CrashWindowMs && crashCount >= MaxConsecutiveRestarts)
            {
                // Reset counter so next manual launch gets a fresh start
                prefs.Edit()?.PutInt(CrashCountPrefKey, 0)?.Apply();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error checking crash loop state");
            return false;
        }
    }

    private static void RecordCrashTimestamp(Activity activity)
    {
        try
        {
            var prefs = activity.GetSharedPreferences("fragment_crash", FileCreationMode.Private);
            if (prefs == null)
            {
                return;
            }

            var lastCrashTime = prefs.GetLong(CrashTimestampPrefKey, 0);
            var now = Java.Lang.JavaSystem.CurrentTimeMillis();

            if (now - lastCrashTime < CrashWindowMs)
            {
                // Within the window — increment counter
                var crashCount = prefs.GetInt(CrashCountPrefKey, 0);
                prefs.Edit()
                    ?.PutLong(CrashTimestampPrefKey, now)
                    ?.PutInt(CrashCountPrefKey, crashCount + 1)
                    ?.Apply();
            }
            else
            {
                // Outside the window — reset counter
                prefs.Edit()
                    ?.PutLong(CrashTimestampPrefKey, now)
                    ?.PutInt(CrashCountPrefKey, 1)
                    ?.Apply();
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error recording crash timestamp");
        }
    }

    /// <summary>
    /// Schedules an app restart using AlarmManager.
    /// Uses exact alarms if permission is granted, otherwise falls back to inexact alarms.
    /// </summary>
    private static void ScheduleAppRestart(Activity activity)
    {
        var packageManager = activity.PackageManager;
        var intent = packageManager?.GetLaunchIntentForPackage(activity.PackageName ?? string.Empty);

        if (intent == null)
        {
            logger.Warning("Could not get launch intent for restart");
            return;
        }

        intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.NewTask | ActivityFlags.ClearTask);

        var pendingIntent = PendingIntent.GetActivity(
            activity,
            0,
            intent,
            PendingIntentFlags.OneShot | PendingIntentFlags.Immutable);

        if (pendingIntent == null)
        {
            logger.Warning("Could not create pending intent for restart");
            return;
        }

        var alarmManager = activity.GetSystemService(Context.AlarmService) as AlarmManager;
        if (alarmManager == null)
        {
            logger.Warning("Could not get AlarmManager for restart");
            return;
        }

        // Schedule restart 500ms from now
        var triggerTime = Java.Lang.JavaSystem.CurrentTimeMillis() + 500;

        // On Android 12+ (API 31+), SCHEDULE_EXACT_ALARM requires user permission grant.
        // Check if we can schedule exact alarms; if not, fall back to inexact alarms.
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            // CanScheduleExactAlarms() is only available on API 31+
            // We've already checked Build.VERSION.SdkInt >= BuildVersionCodes.S, so this is safe
#pragma warning disable CA1416 // Validate platform compatibility
            if (!alarmManager.CanScheduleExactAlarms())
#pragma warning restore CA1416
            {
                // Use inexact alarm - will still trigger within a few seconds
                logger.Information("Using inexact alarm for restart (exact alarm permission not granted)");
                alarmManager.Set(AlarmType.Rtc, triggerTime, pendingIntent);
            }
            else
            {
                // Use exact alarm for more reliable restart timing
                alarmManager.SetExact(AlarmType.Rtc, triggerTime, pendingIntent);
            }
        }
        else
        {
            // On Android < 31, use exact alarm (no permission required)
            alarmManager.SetExact(AlarmType.Rtc, triggerTime, pendingIntent);
        }

        logger.Information("Restart scheduled via AlarmManager");
    }
}
