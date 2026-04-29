#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Services.UI.Interfaces;
using Microsoft.Maui.ApplicationModel;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Helpers;

/// <summary>
/// Service that polls notification permission status and invokes callbacks when permission state changes.
/// </summary>
public sealed class NotificationPermissionPollingService : IDisposable
{
    private static readonly ILogger logger = Log.ForContext<NotificationPermissionPollingService>();
    private CancellationTokenSource? cancellationTokenSource;
    private bool isRunning;
    private volatile bool justRequestedPermission;

    /// <summary>
    /// Callback invoked when permission is granted. Parameter is the current value before update.
    /// </summary>
    public Action<bool>? OnPermissionGranted { get; set; }

    /// <summary>
    /// Callback invoked when permission is denied. Parameter is the current value before update.
    /// </summary>
    public Action<bool>? OnPermissionDenied { get; set; }

    /// <summary>
    /// Gets the current notification enabled value. Used to check current state before updating.
    /// </summary>
    public Func<bool>? GetCurrentValue { get; set; }

    /// <summary>
    /// Sets the notification enabled value. Should update both the property and dispatch state update.
    /// </summary>
    public Action<bool>? SetValue { get; set; }

    /// <summary>
    /// Gets the toast service for showing messages. Optional.
    /// </summary>
    public Func<IToastService?>? GetToastService { get; set; }

    /// <summary>
    /// Starts polling notification permission status.
    /// </summary>
    public void Start()
    {
        if (isRunning)
        {
            logger.Debug("Permission polling already running - stopping existing task");
            Stop();
        }

        cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        isRunning = true;
        justRequestedPermission = false;

        // Request permission if needed (one-time) - do this first before starting the polling loop
        _ = Task.Run(async () =>
        {
            try
            {
                justRequestedPermission = true;
                await NotificationPermissionHelper.RequestNotificationPermissionIfNeededAsync();
                logger.Debug("Permission request completed");
            }
            catch (Exception ex)
            {
                logger.Error(ex, AppConstants.Logging.NotificationPermissionDiagnosticsLog.ErrorRequestingNotificationPermission);
            }
            finally
            {
                // Reset flag after a delay to allow user time to respond
                await Task.Delay(3000);
                justRequestedPermission = false;
            }
        });

        _ = Task.Run(async () =>
        {
            try
            {
                // Wait longer before first check to allow user time to respond to permission dialog
                // Give at least 3 seconds for the user to see and respond to the dialog
                await Task.Delay(3000, cancellationToken);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var granted = NotificationPermissionHelper.IsNotificationPermissionGranted();
                    var currentValue = GetCurrentValue?.Invoke() ?? false;

                    logger.Debug("Permission check: granted={Granted}, currentNotificationEnabled={Current}", granted, currentValue);

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        if (granted)
                        {
                            // Permission granted - toggle ON (internal update, not user action)
                            if (!currentValue)
                            {
                                logger.Information("Notification permission granted - updating toggle to ON. Current value: {Current}", currentValue);
                                OnPermissionGranted?.Invoke(currentValue);
                                SetValue?.Invoke(true);
                            }
                            else
                            {
                                // Permission granted and toggle already ON - task is no longer needed
                                logger.Debug("Permission granted and NotificationEnabled already true - stopping task");
                                cancellationTokenSource?.Cancel();
                                return;
                            }
                        }
                        else
                        {
                            // Permission denied - toggle OFF (internal update, not user action)
                            if (currentValue)
                            {
                                // Don't flip toggle or show toast if we just requested permission
                                // Give the user time to respond to the permission dialog
                                if (justRequestedPermission)
                                {
                                    logger.Debug("Permission denied but we just requested permission - waiting for user response before flipping toggle");
                                }
                                else
                                {
                                    logger.Information("Notification permission denied - updating toggle to OFF. Current value: {Current}", currentValue);
                                    OnPermissionDenied?.Invoke(currentValue);
                                    
                                    // Only show toast when we actually flip the toggle back to OFF
                                    SetValue?.Invoke(false);
                                    
                                    // Show toast message to inform user (only shown when toggle is flipped from ON to OFF)
                                    var toastService = GetToastService?.Invoke();
                                    if (toastService != null)
                                    {
                                        _ = toastService.ShowMessage(AppConstants.ToastMessages.NotificationPermissionDeniedByAndroid, 5);
                                    }
                                }
                            }
                            else
                            {
                                logger.Debug("Permission denied but NotificationEnabled already false - no update needed");
                            }
                        }
                    });

                    // Wait before next check (increased delay to reduce CPU usage)
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                logger.Debug("Permission check task cancelled");
            }
            catch (Exception ex)
            {
                logger.Error(ex, AppConstants.Logging.NotificationPermissionDiagnosticsLog.ErrorInPermissionCheckTask);
            }
            finally
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    isRunning = false;
                });
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Stops polling notification permission status.
    /// </summary>
    public void Stop()
    {
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
            isRunning = false;
            logger.Debug("Stopped permission check task");
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
