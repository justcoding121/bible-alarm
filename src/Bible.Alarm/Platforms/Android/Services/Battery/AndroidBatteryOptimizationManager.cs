using Android.App;
using Android.Content;
using Android.OS;
using Bible.Alarm.Common.Interfaces.Battery;
using Serilog;
using AndroidApplication = Android.App.Application;
using AndroidProvider = Android.Provider;

namespace Bible.Alarm.Platforms.Android.Services.Battery;

public sealed class AndroidBatteryOptimizationManager : IBatteryOptimizationManager
{
    private static readonly ILogger logger = Log.ForContext<AndroidBatteryOptimizationManager>();

    public void ShowBatteryOptimizationExclusionSettingsPage()
    {
        try
        {
            // Ensure we're on the main thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    // Minimum supported is API 26, so this is always available
                    var intent = new Intent(AndroidProvider.Settings.ActionIgnoreBatteryOptimizationSettings);
                    // FLAG_ACTIVITY_NEW_TASK is required when starting an activity from a non-Activity context
                    intent.SetFlags(ActivityFlags.NewTask);
                    AndroidApplication.Context.StartActivity(intent);
                    logger.Information("Successfully opened battery optimization settings page");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to start battery optimization settings activity");
                }
            });
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to show battery optimization dialog.");
        }
    }

    public bool CanShowOptimizeActivity() => Build.VERSION.SdkInt >= BuildVersionCodes.M;

    /// <summary>
    /// Opens the Do Not Disturb settings page where users can add the app to DND exceptions.
    /// This ensures alarms can play even when DND is enabled.
    /// </summary>
    public void ShowDoNotDisturbSettingsPage()
    {
        try
        {
            // Ensure we're on the main thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    // Open notification policy settings (Do Not Disturb settings)
                    // Users need to manually add the app to allowed apps in DND exceptions
                    var intent = new Intent(AndroidProvider.Settings.ActionNotificationPolicyAccessSettings);
                    intent.SetFlags(ActivityFlags.NewTask);
                    AndroidApplication.Context.StartActivity(intent);
                    logger.Information("Successfully opened Do Not Disturb settings page");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to start Do Not Disturb settings activity");
                }
            });
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to show Do Not Disturb settings dialog.");
        }
    }

    /// <summary>
    /// Checks if the app has been granted notification policy access (DND access).
    /// This permission allows the app to bypass Do Not Disturb mode for alarm notifications.
    /// </summary>
    public bool IsNotificationPolicyAccessGranted()
    {
        try
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.M)
            {
                // Notification policy access is only available on Android M (API 23) and above
                return true; // On older versions, assume granted
            }

            var notificationManager = AndroidApplication.Context.GetSystemService(Context.NotificationService) as NotificationManager;
            if (notificationManager == null)
            {
                logger.Warning("NotificationManager is null, cannot check notification policy access");
                return false;
            }

            var isGranted = notificationManager.IsNotificationPolicyAccessGranted;
            logger.Debug("Notification policy access granted: {IsGranted}", isGranted);
            return isGranted;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking notification policy access");
            return false;
        }
    }

    /// <summary>
    /// Checks if the app is currently ignoring battery optimizations (excluded from battery optimization).
    /// Returns true if the app is excluded, false if it's subject to battery optimization.
    /// </summary>
    public bool IsIgnoringBatteryOptimizations()
    {
        try
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.M)
            {
                // Battery optimization is only available on Android M (API 23) and above
                return true; // On older versions, assume excluded
            }

            var powerManager = AndroidApplication.Context.GetSystemService(Context.PowerService) as PowerManager;
            if (powerManager == null)
            {
                logger.Warning("PowerManager is null, cannot check battery optimization status");
                return false;
            }

            var packageName = AndroidApplication.Context.PackageName;
            var isIgnoring = powerManager.IsIgnoringBatteryOptimizations(packageName);
            logger.Debug("Battery optimization ignored (excluded): {IsIgnoring}", isIgnoring);
            return isIgnoring;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking battery optimization status");
            return false;
        }
    }

    public void Dispose()
    {
    }
}
