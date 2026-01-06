using Android.Content;
using Android.OS;
using Bible.Alarm.Common.Interfaces.Battery;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using AndroidApplication = Android.App.Application;
using AndroidProvider = Android.Provider;

namespace Bible.Alarm.Platforms.Android.Services.Battery;

public sealed class AndroidBatteryOptimizationManager : IBatteryOptimizationManager, IDisposable
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

    public void Dispose()
    {
    }
}
