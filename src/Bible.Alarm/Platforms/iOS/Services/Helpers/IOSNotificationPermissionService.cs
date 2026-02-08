#nullable enable

using System;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using UserNotifications;

namespace Bible.Alarm.Platforms.iOS.Services.Helpers;

/// <summary>
/// Event-driven service for managing notification permission on iOS.
/// </summary>
public sealed class IOSNotificationPermissionService : IDisposable
{
    private static readonly ILogger logger = Log.ForContext<IOSNotificationPermissionService>();
    private static IOSNotificationPermissionService? instance;
    private static readonly object instanceLock = new();

    /// <summary>
    /// Gets the singleton instance of the iOS notification permission service.
    /// </summary>
    public static IOSNotificationPermissionService Instance
    {
        get
        {
            if (instance == null)
            {
                lock (instanceLock)
                {
                    instance ??= new IOSNotificationPermissionService();
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
    public bool IsGranted
    {
        get
        {
            try
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UNUserNotificationCenter.Current.GetNotificationSettings(settings =>
                    {
                        var result = settings.AlertSetting == UNNotificationSetting.Enabled;
                        taskCompletionSource.SetResult(result);
                    });
                });

                var granted = taskCompletionSource.Task.Result;
                logger.Debug("IOSNotificationPermissionService.IsGranted: {Granted}", granted);
                return granted;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "IOSNotificationPermissionService.IsGranted: Exception checking permission");
                return false;
            }
        }
    }

    private IOSNotificationPermissionService()
    {
        logger.Debug("IOSNotificationPermissionService: Instance created");
    }

    /// <summary>
    /// Requests notification permission if not already granted.
    /// Returns true if permission is already granted, false if request was initiated.
    /// The PermissionGranted or PermissionDenied event will fire when the user responds.
    /// </summary>
    public bool RequestPermissionIfNeeded()
    {
        // Check if already granted
        if (IsGranted)
        {
            logger.Information("IOSNotificationPermissionService: Permission already granted");
            return true;
        }

        logger.Information("IOSNotificationPermissionService: Requesting notification permission");

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var (granted, error) = await UNUserNotificationCenter.Current.RequestAuthorizationAsync(
                    UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge);

                if (error != null)
                {
                    logger.Error("IOSNotificationPermissionService: Error requesting permission: {Error}", error);
                    PermissionDenied?.Invoke(this, EventArgs.Empty);
                    return;
                }

                logger.Information("IOSNotificationPermissionService: Permission result - Granted: {Granted}", granted);

                if (granted)
                {
                    PermissionGranted?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    PermissionDenied?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "IOSNotificationPermissionService: Exception requesting permission");
                PermissionDenied?.Invoke(this, EventArgs.Empty);
            }
        });

        return false;
    }

    public void Dispose()
    {
        // Clear event handlers
        PermissionGranted = null;
        PermissionDenied = null;
    }
}
