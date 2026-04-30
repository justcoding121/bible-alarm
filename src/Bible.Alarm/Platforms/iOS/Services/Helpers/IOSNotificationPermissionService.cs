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

    private bool? cachedPermissionResult;
    private DateTime lastPermissionCheck = DateTime.MinValue;
    private readonly TimeSpan permissionCacheTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets whether notification permission is currently granted.
    /// Uses cached result if available and recent.
    /// When cache is expired but a previous result exists, returns the stale result
    /// while refreshing in the background (avoids false negatives).
    /// When no result exists at all, returns false and starts an async check.
    /// </summary>
    public bool IsGranted
    {
        get
        {
            try
            {
                // Return cached result if available and recent
                if (cachedPermissionResult.HasValue && 
                    DateTime.UtcNow - lastPermissionCheck < permissionCacheTimeout)
                {
                    logger.Debug("[NOTIFICATION-PERMISSION] Using cached result: {Result}", cachedPermissionResult.Value);
                    return cachedPermissionResult.Value;
                }

                // Cache expired or missing - start async refresh
                logger.Debug("[NOTIFICATION-PERMISSION] Cache expired or missing, starting async refresh");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await IOSNotificationPermissionService.IsGrantedAsync();
                        cachedPermissionResult = result;
                        lastPermissionCheck = DateTime.UtcNow;
                        logger.Debug("[NOTIFICATION-PERMISSION] Cache refreshed with result: {Result}", result);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "[NOTIFICATION-PERMISSION] Error refreshing permission cache");
                    }
                });

                // Return the previous cached value if available (even if expired),
                // otherwise false as safe default for first-ever check.
                // This prevents returning false when permission IS granted but cache just expired.
                var fallback = cachedPermissionResult ?? false;
                logger.Debug("[NOTIFICATION-PERMISSION] Returning fallback while refreshing: {Result}", fallback);
                return fallback;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "IOSNotificationPermissionService.IsGranted: Exception checking permission");
                return false;
            }
        }
    }

    /// <summary>
    /// Gets whether notification permission is currently granted (async version).
    /// </summary>
    public static async Task<bool> IsGrantedAsync()
    {
        try
        {
            logger.Debug("[NOTIFICATION-PERMISSION] IsGrantedAsync: Starting async check");
            
            var result = await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();

                UNUserNotificationCenter.Current.GetNotificationSettings(settings =>
                {
                    var isEnabled = settings.AlertSetting == UNNotificationSetting.Enabled;
                    logger.Debug("[NOTIFICATION-PERMISSION] IsGrantedAsync: AlertSetting={AlertSetting}, IsEnabled={IsEnabled}",
                        settings.AlertSetting, isEnabled);
                    taskCompletionSource.SetResult(isEnabled);
                });

                return taskCompletionSource.Task;
            });
            
            logger.Debug("[NOTIFICATION-PERMISSION] IsGrantedAsync: Result={Result}", result);
            return result;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[NOTIFICATION-PERMISSION] IsGrantedAsync: Exception checking permission");
            return false;
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
        // Check if already granted using synchronous check (cache should be up-to-date from events)
        // This matches Android's behavior where IsGranted always checks actual status
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
                    UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge | UNAuthorizationOptions.TimeSensitive);

                if (error != null)
                {
                    logger.Error("IOSNotificationPermissionService: Error requesting permission: {Error}", error);
                    // Update cache
                    cachedPermissionResult = false;
                    lastPermissionCheck = DateTime.UtcNow;
                    PermissionDenied?.Invoke(this, EventArgs.Empty);
                    return;
                }

                logger.Information("IOSNotificationPermissionService: Permission result - Granted: {Granted}", granted);

                // Update cache immediately when permission changes
                // This ensures IsGranted property returns correct value immediately
                cachedPermissionResult = granted;
                lastPermissionCheck = DateTime.UtcNow;
                logger.Information("[NOTIFICATION-PERMISSION] Cache updated after permission request - Granted: {Granted}", granted);

                // Fire events on main thread (like Android does)
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
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
                        logger.Error(ex, "IOSNotificationPermissionService: Exception in permission event handler");
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "IOSNotificationPermissionService: Exception requesting permission");
                // Update cache
                cachedPermissionResult = false;
                lastPermissionCheck = DateTime.UtcNow;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        PermissionDenied?.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception eventEx)
                    {
                        logger.Error(eventEx, "IOSNotificationPermissionService: Exception in PermissionDenied event handler");
                    }
                });
            }
        });

        return false;
    }
    
    /// <summary>
    /// Returns true if the OS can still show the system permission prompt (user has not denied yet).
    /// On iOS the system prompt is shown only once; after the user taps "Don't Allow" this returns false.
    /// </summary>
    public static async Task<bool> CanShowSystemPromptAsync()
    {
        try
        {
            var result = await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var tcs = new TaskCompletionSource<bool>();
                UNUserNotificationCenter.Current.GetNotificationSettings(settings =>
                {
                    var status = settings.AuthorizationStatus;
                    var canShow = status == UNAuthorizationStatus.NotDetermined;
                    logger.Debug("[NOTIFICATION-PERMISSION] CanShowSystemPromptAsync: AuthorizationStatus={Status}, CanShow={CanShow}", status, canShow);
                    tcs.SetResult(canShow);
                });
                return tcs.Task;
            });
            return result;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[NOTIFICATION-PERMISSION] CanShowSystemPromptAsync: Exception");
            return false;
        }
    }

    /// <summary>
    /// Invalidates the permission cache timeout, forcing a fresh async check on next access.
    /// Preserves the previous cached result so IsGranted can return it as a fallback
    /// (avoids returning false when permission is actually granted).
    /// </summary>
    public void InvalidateCache()
    {
        // Only reset the timestamp, NOT the cached result.
        // IsGranted will return the stale result while refreshing in the background.
        lastPermissionCheck = DateTime.MinValue;
        logger.Debug("[NOTIFICATION-PERMISSION] Cache timeout invalidated (previous result preserved as fallback)");
    }

    public void Dispose()
    {
        // Clear event handlers
        PermissionGranted = null;
        PermissionDenied = null;
    }
}
