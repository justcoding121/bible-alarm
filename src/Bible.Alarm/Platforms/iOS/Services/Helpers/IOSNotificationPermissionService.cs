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
    /// Uses cached result if available and recent, otherwise returns false (safe default).
    /// The actual permission check happens asynchronously via IsGrantedAsync().
    /// </summary>
    public bool IsGranted
    {
        get
        {
            try
            {
                // Return cached result if available and recent
                if (cachedPermissionResult.HasValue && 
                    DateTime.Now - lastPermissionCheck < permissionCacheTimeout)
                {
                logger.Information("[NOTIFICATION-PERMISSION] Using cached result: {Result}", cachedPermissionResult.Value);
                    return cachedPermissionResult.Value;
                }

                logger.Information("[NOTIFICATION-PERMISSION] No cached result, starting async check");

                // Start async check to update cache (fire and forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await IsGrantedAsync();
                        cachedPermissionResult = result;
                        lastPermissionCheck = DateTime.Now;
                        logger.Information("[NOTIFICATION-PERMISSION] Cache updated with result: {Result}", result);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "[NOTIFICATION-PERMISSION] Error updating permission cache");
                    }
                });

                // Return false as safe default until async check completes
                logger.Information("[NOTIFICATION-PERMISSION] Returning false as safe default (no cache)");
                return cachedPermissionResult ?? false;
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
    public async Task<bool> IsGrantedAsync()
    {
        try
        {
            logger.Information("[NOTIFICATION-PERMISSION] IsGrantedAsync: Starting async check");
            
            var result = await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();

                logger.Information("[NOTIFICATION-PERMISSION] IsGrantedAsync: Calling GetNotificationSettings");
                
                UNUserNotificationCenter.Current.GetNotificationSettings(settings =>
                {
                    var isEnabled = settings.AlertSetting == UNNotificationSetting.Enabled;
                    logger.Information("[NOTIFICATION-PERMISSION] IsGrantedAsync: Callback - AlertSetting={AlertSetting}, IsEnabled={IsEnabled}",
                        settings.AlertSetting, isEnabled);
                    taskCompletionSource.SetResult(isEnabled);
                });

                return taskCompletionSource.Task;
            });
            
            logger.Information("[NOTIFICATION-PERMISSION] IsGrantedAsync: Final result={Result}", result);
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
                    UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge);

                if (error != null)
                {
                    logger.Error("IOSNotificationPermissionService: Error requesting permission: {Error}", error);
                    // Update cache
                    cachedPermissionResult = false;
                    lastPermissionCheck = DateTime.Now;
                    PermissionDenied?.Invoke(this, EventArgs.Empty);
                    return;
                }

                logger.Information("IOSNotificationPermissionService: Permission result - Granted: {Granted}", granted);

                // Update cache immediately when permission changes
                // This ensures IsGranted property returns correct value immediately
                cachedPermissionResult = granted;
                lastPermissionCheck = DateTime.Now;
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
                lastPermissionCheck = DateTime.Now;
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
    /// Invalidates the permission cache, forcing a fresh check on next access.
    /// </summary>
    public void InvalidateCache()
    {
        cachedPermissionResult = null;
        lastPermissionCheck = DateTime.MinValue;
        logger.Debug("[NOTIFICATION-PERMISSION] Cache invalidated");
    }

    public void Dispose()
    {
        // Clear event handlers
        PermissionGranted = null;
        PermissionDenied = null;
    }
}
