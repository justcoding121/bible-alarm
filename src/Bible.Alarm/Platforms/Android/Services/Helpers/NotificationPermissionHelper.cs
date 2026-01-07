#nullable enable
using Android;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Bible.Alarm.Common;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using AndroidApplication = Android.App.Application;

namespace Bible.Alarm.Platforms.Android.Services.Helpers;

/// <summary>
/// Helper for checking and requesting notification permission on Android 13+.
/// </summary>
public static class NotificationPermissionHelper
{
    private static readonly ILogger logger = Log.ForContext(typeof(NotificationPermissionHelper));

    /// <summary>
    /// Checks if notification permission is granted.
    /// </summary>
    public static bool IsNotificationPermissionGranted()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            // Permission not required on Android 12 and below
            logger.Debug("Android version < 33 - notification permission not required");
            return true;
        }

        var sdkInt = Build.VERSION.SdkInt;
        logger.Debug("Checking notification permission - Android SDK: {SdkInt}, Tiramisu: {Tiramisu}", sdkInt, BuildVersionCodes.Tiramisu);

        var context = AndroidApplication.Context;
        var result = ContextCompat.CheckSelfPermission(context, Manifest.Permission.PostNotifications);
        var granted = result == Permission.Granted;
        logger.Debug("Notification permission check result: {Result} (Granted={Granted})", result, granted);
        return granted;
    }

    /// <summary>
    /// Requests notification permission if not already granted.
    /// Returns true if permission is already granted or was just granted, false if denied.
    /// </summary>
    public static async Task<bool> RequestNotificationPermissionIfNeededAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            // Permission not required on Android 12 and below
            logger.Debug("RequestNotificationPermissionIfNeededAsync - Android version < 33 - notification permission not required");
            return true;
        }

        var sdkInt = Build.VERSION.SdkInt;
        logger.Debug("RequestNotificationPermissionIfNeededAsync - Android SDK: {SdkInt}", sdkInt);

        // Check if already granted
        if (IsNotificationPermissionGranted())
        {
            logger.Information("Notification permission already granted - no request needed");
            return true;
        }

        logger.Information("Requesting POST_NOTIFICATIONS permission (Android 13+)");

        // Get the current activity using MAUI Platform (fully qualified to avoid namespace conflicts)
        var activity = global::Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity == null)
        {
            logger.Warning("Cannot request notification permission - CurrentActivity is null");
            return false;
        }

        logger.Debug("CurrentActivity found: {ActivityType}", activity.GetType().Name);

        // Request permission using ActivityCompat on main thread
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                logger.Debug("Calling ActivityCompat.RequestPermissions for POST_NOTIFICATIONS");
                ActivityCompat.RequestPermissions(activity, new[] { Manifest.Permission.PostNotifications }, 0);
                logger.Debug("ActivityCompat.RequestPermissions called - permission dialog should appear");
            }
        });

        // Wait a bit for the permission dialog to appear and user to respond
        // Then check the status
        logger.Debug("Waiting 500ms for user to respond to permission dialog");
        await Task.Delay(500);

        // Check permission status after request
        var granted = IsNotificationPermissionGranted();
        logger.Information("Notification permission request result: Granted: {Granted}", granted);
        
        return granted;
    }
}
