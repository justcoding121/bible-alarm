#nullable enable

using System.Windows.Input;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.UI.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using Bible.Alarm.ViewModels;

#if ANDROID
using Bible.Alarm.Platforms.Android.Services.Helpers;
using Android.Content;
using Android.Provider;
using AndroidApplication = Android.App.Application;
#elif IOS
using Bible.Alarm.Platforms.iOS.Services.Helpers;
#endif

namespace Bible.Alarm.ViewModels.General;

public sealed class NotificationPermissionViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
    private readonly IServiceProvider serviceProvider;

#if ANDROID
    private readonly NotificationPermissionService? permissionService;
#elif IOS
    private readonly IOSNotificationPermissionService? permissionService;
#endif

    private bool isNotificationPermissionGranted;
    private System.Timers.Timer? permissionCheckTimer;
    private readonly Action<bool>? onModalDismissed;
    private bool isDismissing;

    public NotificationPermissionViewModel(
        ILogger logger,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        Action<bool>? onModalDismissed = null)
    {
        this.logger = logger;
        this.navigationService = navigationService;
        this.serviceProvider = serviceProvider;
        this.onModalDismissed = onModalDismissed;

#if ANDROID
        permissionService = NotificationPermissionService.Instance;
#elif IOS
        permissionService = IOSNotificationPermissionService.Instance;
#endif

        InitializeCommands();
        InitializePermissionStatus();
    }

    private void InitializeCommands()
    {
        RequestNotificationPermissionCommand = new AsyncRelayCommand(async () =>
        {
#if ANDROID
            if (DeviceInfo.Platform == DevicePlatform.Android && permissionService != null)
            {
                // Request permission - will fire PermissionGranted or PermissionDenied event
                permissionService.RequestPermissionIfNeeded();
            }
#elif IOS
            if (DeviceInfo.Platform == DevicePlatform.iOS && permissionService != null)
            {
                // Request permission - will fire PermissionGranted or PermissionDenied event
                permissionService.RequestPermissionIfNeeded();
            }
#endif
        });

        OpenSettingsCommand = new AsyncRelayCommand(async () =>
        {
#if ANDROID
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                // Open Android app settings using proper Intent
                try
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            var intent = new Intent(Settings.ActionApplicationDetailsSettings);
                            var uri = Android.Net.Uri.FromParts("package", AndroidApplication.Context.PackageName, null);
                            intent.SetData(uri);
                            intent.SetFlags(ActivityFlags.NewTask);
                            AndroidApplication.Context.StartActivity(intent);
                            logger.Information("Successfully opened Android app settings");
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, "Failed to open Android app settings");
                        }
                    });
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to open Android app settings");
                }
            }
#elif IOS
            if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
                // Open iOS app settings
                await Launcher.OpenAsync(new Uri("app-settings:"));
            }
#endif
        });

        DismissCommand = new AsyncRelayCommand(async () =>
        {
            // Prevent double-dismiss (race between timer polling and OnPermissionGranted event)
            if (isDismissing)
            {
                logger.Debug("DismissCommand: Already dismissing, skipping duplicate call");
                return;
            }
            isDismissing = true;

            try
            {
                StopPermissionCheckTimer();
                
                // Check permission status before closing modal
                CheckPermissionStatus();
                var wasGranted = IsNotificationPermissionGranted;
                
                await navigationService.PopModalAsync();
                UpdateHomePageButtonVisibility();
                
                // Call callback if provided (e.g., from schedule page to update toggle)
                if (onModalDismissed != null)
                {
                    try
                    {
                        onModalDismissed(wasGranted);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error in onModalDismissed callback");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in DismissCommand");
                isDismissing = false;
            }
        });
    }

    private void InitializePermissionStatus()
    {
        CheckPermissionStatus();
    }

    private void CheckPermissionStatus()
    {
        try
        {
            logger.Debug("Checking notification permission status...");
            var wasGranted = IsNotificationPermissionGranted;

#if ANDROID
            if (DeviceInfo.Platform == DevicePlatform.Android && permissionService != null)
            {
                IsNotificationPermissionGranted = permissionService.IsGranted;
            }
#elif IOS
            if (DeviceInfo.Platform == DevicePlatform.iOS && permissionService != null)
            {
                // For iOS, use synchronous check which uses cache
                // Cache is updated when PermissionGranted/PermissionDenied events fire
                // This matches Android's behavior where IsGranted always checks actual status
                IsNotificationPermissionGranted = permissionService.IsGranted;
            }
#endif

            logger.Debug("Permission check completed - Granted: {IsGranted} (was {WasGranted})",
                IsNotificationPermissionGranted, wasGranted);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking notification permission status");
        }
    }

    public void StartPermissionCheckTimer()
    {
        // Stop any existing timer before starting a new one
        StopPermissionCheckTimer();

        logger.Debug("Starting permission check timer for notification permission modal");

#if ANDROID
        if (DeviceInfo.Platform == DevicePlatform.Android && permissionService != null)
        {
            // Subscribe to permission events
            permissionService.PermissionGranted += OnPermissionGranted;
            permissionService.PermissionDenied += OnPermissionDenied;
        }
#elif IOS
        if (DeviceInfo.Platform == DevicePlatform.iOS && permissionService != null)
        {
            // Subscribe to permission events
            permissionService.PermissionGranted += OnPermissionGranted;
            permissionService.PermissionDenied += OnPermissionDenied;
        }
#endif

        // Check permissions every 1 second while modal is open
        permissionCheckTimer = new System.Timers.Timer(1000);
        permissionCheckTimer.Elapsed += OnPermissionCheckTimerElapsed;
        permissionCheckTimer.AutoReset = true;
        permissionCheckTimer.Start();
        logger.Debug("Permission check timer started successfully");
    }

    private void OnPermissionCheckTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // Skip if already dismissing
        if (isDismissing)
        {
            return;
        }

        var wasGranted = IsNotificationPermissionGranted;

#if IOS
        if (permissionService != null)
        {
            // iOS: Use async check to get fresh status (e.g., when user returns from Settings)
            _ = Task.Run(async () =>
            {
                try
                {
                    permissionService.InvalidateCache();
                    var result = await permissionService.IsGrantedAsync();
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        IsNotificationPermissionGranted = result;
                        logger.Debug("Permission check (async) completed - Granted: {IsGranted} (was {WasGranted})",
                            IsNotificationPermissionGranted, wasGranted);
                        
                        if (!wasGranted && IsNotificationPermissionGranted)
                        {
                            ScheduleAutoDismissOnMainThread();
                        }
                    });
                }
                catch (Exception asyncEx)
                {
                    logger.Error(asyncEx, "Error in async permission check from timer");
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        CheckPermissionStatus();
                        if (!wasGranted && IsNotificationPermissionGranted)
                        {
                            ScheduleAutoDismissOnMainThread();
                        }
                    });
                }
            });
            return;
        }
#endif

        // Android and other platforms: use synchronous check on main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CheckPermissionStatus();
            if (!wasGranted && IsNotificationPermissionGranted)
            {
                ScheduleAutoDismissOnMainThread();
            }
        });
    }

    /// <summary>
    /// Schedules auto-dismiss on the main thread after a brief delay.
    /// Must be called from the main thread.
    /// </summary>
    private void ScheduleAutoDismissOnMainThread()
    {
        if (isDismissing)
        {
            return;
        }

        logger.Information("Permission granted detected by polling - scheduling auto-dismiss");
        
        // Use MainThread.InvokeOnMainThreadAsync to ensure dismiss runs on main thread
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await Task.Delay(500);
                if (DismissCommand is AsyncRelayCommand asyncCommand)
                {
                    await asyncCommand.ExecuteAsync(null);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in scheduled auto-dismiss");
            }
        });
    }

    private void OnPermissionGranted(object? sender, EventArgs e)
    {
        logger.Information("Notification permission granted event received");
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Update permission status immediately
            CheckPermissionStatus();
            
            // If permission is now granted, auto-dismiss the modal after a short delay
            if (IsNotificationPermissionGranted)
            {
                logger.Information("Permission granted - auto-dismissing modal in 1 second");
                await Task.Delay(1000);
                
                // Double-check permission status and dismissing flag before dismissing
                CheckPermissionStatus();
                
                if (IsNotificationPermissionGranted && !isDismissing)
                {
                    if (DismissCommand is AsyncRelayCommand asyncCommand)
                    {
                        await asyncCommand.ExecuteAsync(null);
                    }
                }
            }
        });
    }

    private void OnPermissionDenied(object? sender, EventArgs e)
    {
        logger.Information("Notification permission denied event received");
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CheckPermissionStatus();
        });
    }

    private void StopPermissionCheckTimer()
    {
        if (permissionCheckTimer != null)
        {
            permissionCheckTimer.Stop();
            permissionCheckTimer.Elapsed -= OnPermissionCheckTimerElapsed;
            permissionCheckTimer.Dispose();
            permissionCheckTimer = null;
        }

#if ANDROID
        if (DeviceInfo.Platform == DevicePlatform.Android && permissionService != null)
        {
            // Unsubscribe from permission events
            permissionService.PermissionGranted -= OnPermissionGranted;
            permissionService.PermissionDenied -= OnPermissionDenied;
        }
#elif IOS
        if (DeviceInfo.Platform == DevicePlatform.iOS && permissionService != null)
        {
            // Unsubscribe from permission events
            permissionService.PermissionGranted -= OnPermissionGranted;
            permissionService.PermissionDenied -= OnPermissionDenied;
        }
#endif
    }

    private void UpdateHomePageButtonVisibility()
    {
        try
        {
            // Get the Home page from navigation service
            var homePage = navigationService.GetCurrentHomePage();
            if (homePage?.BindingContext is HomeViewModel homeViewModel)
            {
                // Update the notification permission button visibility based on current permissions
                // This follows the same pattern as BatteryOptimizationViewModel.UpdateHomePageButtonVisibility()
                homeViewModel.UpdateNotificationPermissionButtonVisibility();
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error updating home page button visibility after modal close");
        }
    }

    public bool IsNotificationPermissionGranted
    {
        get => isNotificationPermissionGranted;
        set
        {
            if (SetProperty(ref isNotificationPermissionGranted, value))
            {
                OnPropertyChanged(nameof(IsRequestButtonVisible));
                OnPropertyChanged(nameof(IsOpenSettingsButtonVisible));
                OnPropertyChanged(nameof(IsInstructionsVisible));
            }
        }
    }

    public string RequestButtonText => "REQUEST NOTIFICATION PERMISSION";

    public string OpenSettingsButtonText
    {
        get
        {
#if ANDROID
            return "OPEN APP SETTINGS";
#elif IOS
            return "OPEN SETTINGS";
#else
            return "OPEN SETTINGS";
#endif
        }
    }

    public string MainMessage
    {
        get
        {
#if ANDROID
            return "Notification permission is required for tap-to-play alarms. Please enable notifications to allow the app to show reminder notifications that you can tap to play alarms.";
#elif IOS
            return "Notification permission is required for alarms to work on iOS. Please enable notifications to allow the app to play alarms at scheduled times.";
#else
            return "Notification permission is required for alarms. Please enable notifications.";
#endif
        }
    }

    public string InstructionsText
    {
        get
        {
#if ANDROID
            return "After clicking the button below, tap 'Allow' in the system permission dialog to enable notifications. If you've previously denied permission, use 'Open App Settings' to enable it in system settings.";
#elif IOS
            return "After clicking the button below, tap 'Allow' in the system permission dialog to enable notifications. If you've previously denied permission, use 'Open Settings' to enable it in system settings.";
#else
            return "Please enable notifications in system settings.";
#endif
        }
    }

    /// <summary>
    /// Shows the request button only if notification permission is not granted and can be requested.
    /// </summary>
    public bool IsRequestButtonVisible => !IsNotificationPermissionGranted;

    /// <summary>
    /// Shows the open settings button if permission is not granted (user may have denied it previously).
    /// </summary>
    public bool IsOpenSettingsButtonVisible => !IsNotificationPermissionGranted;

    /// <summary>
    /// Shows the instructions label only if permission is not granted (i.e., there's something to configure).
    /// </summary>
    public bool IsInstructionsVisible => !IsNotificationPermissionGranted;

    public ICommand RequestNotificationPermissionCommand { get; private set; } = null!;
    public ICommand OpenSettingsCommand { get; private set; } = null!;
    public ICommand DismissCommand { get; private set; } = null!;

    public void Dispose()
    {
        StopPermissionCheckTimer();
    }
}
