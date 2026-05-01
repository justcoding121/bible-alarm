#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Android;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using AndroidApplication = Android.App.Application;

namespace Bible.Alarm.Platforms.Android.Services.Helpers;

/// <summary>
/// Event-driven service for managing notification permission on Android 13+.
/// Uses Android's permission callback system instead of polling.
/// </summary>
public sealed class NotificationPermissionService : IDisposable
{
    private static readonly ILogger logger = Log.ForContext<NotificationPermissionService>();
    private static NotificationPermissionService? instance;
    private static readonly object instanceLock = new();
    private bool promptExhausted;

    /// <summary>
    /// Gets the singleton instance of the notification permission service.
    /// </summary>
    public static NotificationPermissionService Instance
    {
        get
        {
            if (instance == null)
            {
                lock (instanceLock)
                {
                    instance ??= new NotificationPermissionService();
                }
            }
            return instance;
        }
    }

    /// <summary>
    /// Event fired when notification permission is granted.
    /// </summary>
    public event EventHandler? PermissionGranted;

    /// <summary>
    /// Event fired when notification permission is denied.
    /// </summary>
    public event EventHandler? PermissionDenied;

    /// <summary>
    /// Gets whether notification permission is currently granted.
    /// </summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keeps symmetrical API with iOS notification singleton and instance event surface.")]
    [SuppressMessage("SonarAnalyzer.CSharp", "S2325", Justification = "Same as CA1822.")]
    public bool IsGranted
    {
        get
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                // Permission not required on Android 12 and below
                return true;
            }

            try
            {
                var context = AndroidApplication.Context;
                if (context == null)
                {
                    logger.Warning("NotificationPermissionService.IsGranted: AndroidApplication.Context is null");
                    return false;
                }

                return TryGetPostNotificationsGranted(context);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "NotificationPermissionService.IsGranted: Exception getting context or checking permission");
                return false;
            }
        }
    }

    /// <summary>
    /// Wraps permission check to handle invalid context during lifecycle transitions.
    /// </summary>
    private bool TryGetPostNotificationsGranted(global::Android.Content.Context context)
    {
        try
        {
            var result = ContextCompat.CheckSelfPermission(context, Manifest.Permission.PostNotifications);
            var granted = result == Permission.Granted;
            logger.Debug("NotificationPermissionService.IsGranted: {Granted}", granted);
            return granted;
        }
        catch (Java.Lang.RuntimeException javaEx)
        {
            logger.Error(javaEx, "NotificationPermissionService.IsGranted: Java exception checking permission (context may be invalid)");
            return false;
        }
        catch (System.Exception ex)
        {
            logger.Error(ex, "NotificationPermissionService.IsGranted: Exception checking permission");
            return false;
        }
    }

    private NotificationPermissionService()
    {
        logger.Debug("NotificationPermissionService: Instance created");
    }

    /// <summary>
    /// Requests notification permission if not already granted.
    /// Returns true if permission is already granted, false if request was initiated.
    /// The PermissionGranted or PermissionDenied event will fire when the user responds.
    /// </summary>
    public bool RequestPermissionIfNeeded()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            // Permission not required on Android 12 and below
            logger.Debug("NotificationPermissionService: Android version < 33 - permission not required");
            return true;
        }

        // Check if already granted
        if (IsGranted)
        {
            logger.Information("NotificationPermissionService: Permission already granted");
            return true;
        }

        logger.Information("NotificationPermissionService: Requesting POST_NOTIFICATIONS permission");

        // Get the current activity using MAUI Platform (fully qualified to avoid namespace conflicts)
        var activity = global::Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity == null)
        {
            logger.Warning("NotificationPermissionService: Cannot request permission - CurrentActivity is null");
            return false;
        }

        logger.Debug("NotificationPermissionService: CurrentActivity found: {ActivityType}", activity.GetType().Name);

        // Request permission using ActivityCompat on main thread
        // The result will be handled by MainActivity.OnRequestPermissionsResult
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (OperatingSystem.IsAndroidVersionAtLeast(33))
                {
                    // Re-check activity is still valid (could have been destroyed between check and use)
                    var currentActivity = global::Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                    if (currentActivity == null)
                    {
                        logger.Warning("NotificationPermissionService: CurrentActivity became null before requesting permission");
                        return;
                    }

                    logger.Debug("NotificationPermissionService: Calling ActivityCompat.RequestPermissions");
                    AndroidX.Core.App.ActivityCompat.RequestPermissions(
                        currentActivity,
                        new[] { Manifest.Permission.PostNotifications },
                        NotificationPermissionRequestCode);
                    logger.Debug("NotificationPermissionService: Permission dialog should appear");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "NotificationPermissionService: Exception requesting permission");
            }
        });

        return false;
    }

    /// <summary>
    /// Returns true if the OS may still show the system permission prompt.
    /// After the user selects "Don't ask again" or denies repeatedly, this returns false.
    /// </summary>
    public bool CanShowSystemPrompt => !IsGranted && !promptExhausted;

    /// <summary>
    /// Request code used for notification permission requests.
    /// Must match the code used in MainActivity.OnRequestPermissionsResult.
    /// </summary>
    public const int NotificationPermissionRequestCode = 1001;

    /// <summary>
    /// Handles permission request result from MainActivity.
    /// This is called by MainActivity.OnRequestPermissionsResult.
    /// </summary>
    internal void HandlePermissionResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        if (requestCode != NotificationPermissionRequestCode)
        {
            return;
        }

        if (permissions.Length == 0 || grantResults.Length == 0)
        {
            logger.Warning("NotificationPermissionService: Empty permissions or grantResults array");
            return;
        }

        var permission = permissions[0];
        var grantResult = grantResults[0];

        if (permission != Manifest.Permission.PostNotifications)
        {
            logger.Debug("NotificationPermissionService: Ignoring permission result for {Permission}", permission);
            return;
        }

        var granted = grantResult == Permission.Granted;
        logger.Information("NotificationPermissionService: Permission result - Granted: {Granted}", granted);

        try
        {
            if (granted)
            {
                InvokePermissionGrantedOnMainThread();
            }
            else
            {
                InvokePermissionDeniedOnMainThread();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "NotificationPermissionService: Exception invoking permission result events");
        }
    }

    private void InvokePermissionGrantedOnMainThread()
    {
        promptExhausted = false;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                PermissionGranted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "NotificationPermissionService: Exception in PermissionGranted event handler");
            }
        });
    }

    private void InvokePermissionDeniedOnMainThread()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                UpdatePromptExhaustedIfPermanentDenial();
                PermissionDenied?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "NotificationPermissionService: Exception in PermissionDenied event handler");
            }
        });
    }

    private void UpdatePromptExhaustedIfPermanentDenial()
    {
        var activity = global::Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity != null && (int)Build.VERSION.SdkInt >= 33 &&
            !ActivityCompat.ShouldShowRequestPermissionRationale(activity, "android.permission.POST_NOTIFICATIONS"))
        {
            promptExhausted = true;
            logger.Information("NotificationPermissionService: System prompt exhausted (user permanently denied or don't ask again)");
        }
    }

    public void Dispose()
    {
        // Clear event handlers
        PermissionGranted = null;
        PermissionDenied = null;
    }
}
