#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Shared.Constants;
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

#if ANDROID
    private readonly NotificationPermissionService? permissionService;
#elif IOS
    private readonly IosNotificationPermissionService? permissionService;
#endif

    private bool isNotificationPermissionGranted;
    private bool canShowSystemPrompt = true;
    private System.Timers.Timer? permissionCheckTimer;
    private readonly Action<bool>? onModalDismissed;
    private bool isDismissing;

    public NotificationPermissionViewModel(
        ILogger logger,
        INavigationService navigationService,
        Action<bool>? onModalDismissed = null)
    {
        this.logger = logger;
        this.navigationService = navigationService;
        this.onModalDismissed = onModalDismissed;

#if ANDROID
        permissionService = NotificationPermissionService.Instance;
#elif IOS
        permissionService = IosNotificationPermissionService.Instance;
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
                            logger.Information(AppConstants.Logging.NotificationPermissionDiagnosticsLog.SuccessfullyOpenedAndroidAppSettings);
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, AppConstants.Logging.NotificationPermissionDiagnosticsLog.FailedToOpenAndroidAppSettings);
                        }
                    });
                }
                catch (Exception ex)
                {
                    logger.Error(ex, AppConstants.Logging.NotificationPermissionDiagnosticsLog.FailedToOpenAndroidAppSettings);
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
                logger.Debug(AppConstants.Logging.NotificationPermissionDiagnosticsLog.DismissCommandAlreadyDismissingSkippingDuplicate);
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
                        logger.Error(ex, AppConstants.Logging.NotificationPermissionDiagnosticsLog.ErrorInOnModalDismissedCallback);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, AppConstants.Logging.NotificationPermissionDiagnosticsLog.ErrorInDismissCommand);
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
            logger.Debug(AppConstants.Logging.NotificationPermissionDiagnosticsLog.CheckingNotificationPermissionStatus);
            var wasGranted = IsNotificationPermissionGranted;

#if ANDROID
            if (DeviceInfo.Platform == DevicePlatform.Android && permissionService != null)
            {
                IsNotificationPermissionGranted = permissionService.IsGranted;
                UpdateCanShowSystemPrompt(permissionService.CanShowSystemPrompt);
            }
#elif IOS
            if (DeviceInfo.Platform == DevicePlatform.iOS && permissionService != null)
            {
                IsNotificationPermissionGranted = permissionService.IsGranted;
                _ = RefreshCanShowSystemPromptAsync();
            }
#endif

            logger.Debug(AppConstants.Logging.NotificationPermissionDiagnosticsLog.PermissionCheckCompletedGrantedWasGranted,
                IsNotificationPermissionGranted, wasGranted);
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.NotificationPermissionDiagnosticsLog.ErrorCheckingNotificationPermissionStatus);
        }
    }

    public void StartPermissionCheckTimer()
    {
        // Stop any existing timer before starting a new one
        StopPermissionCheckTimer();

        logger.Debug(AppConstants.Logging.NotificationPermissionDiagnosticsLog.StartingPermissionCheckTimerForModal);

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
#if IOS
        _ = RefreshCanShowSystemPromptAsync();
#endif
        logger.Debug(AppConstants.Logging.NotificationPermissionDiagnosticsLog.PermissionCheckTimerStartedSuccessfully);
    }

#if IOS
    private async Task RefreshCanShowSystemPromptAsync()
    {
        if (permissionService == null)
            return;
        try
        {
            var canShow = await IosNotificationPermissionService.CanShowSystemPromptAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateCanShowSystemPrompt(canShow);
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.NotificationPermissionDiagnosticsLog.ErrorRefreshingCanShowSystemPromptIos);
        }
    }
#endif

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
            _ = Task.Run(async () =>
            {
                try
                {
                    permissionService.InvalidateCache();
                    var result = await IosNotificationPermissionService.IsGrantedAsync();
                    var canShow = await IosNotificationPermissionService.CanShowSystemPromptAsync();
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        IsNotificationPermissionGranted = result;
                        UpdateCanShowSystemPrompt(canShow);
                        logger.Debug(AppConstants.Logging.NotificationPermissionDiagnosticsLog.PermissionCheckAsyncCompletedGrantedCanShowWasGranted,
                            IsNotificationPermissionGranted, canShow, wasGranted);
                        if (!wasGranted && IsNotificationPermissionGranted)
                        {
                            ScheduleAutoDismissOnMainThread();
                        }
                    });
                }
                catch (Exception asyncEx)
                {
                    logger.Error(asyncEx, AppConstants.Logging.NotificationPermissionDiagnosticsLog.ErrorInAsyncPermissionCheckFromTimer);
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

        // Android: use synchronous check on main thread (also refreshes CanShowSystemPrompt)
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

        logger.Information(AppConstants.Logging.NotificationPermissionDiagnosticsLog.PermissionGrantedDetectedByPollingSchedulingAutoDismiss);
        
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
                logger.Error(ex, AppConstants.Logging.NotificationPermissionDiagnosticsLog.ErrorInScheduledAutoDismiss);
            }
        });
    }

    private void OnPermissionGranted(object? sender, EventArgs e)
    {
        logger.Information(AppConstants.Logging.NotificationPermissionDiagnosticsLog.NotificationPermissionGrantedEventReceived);
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Update permission status immediately
            CheckPermissionStatus();
            
            // If permission is now granted, auto-dismiss the modal after a short delay
            if (IsNotificationPermissionGranted)
            {
                logger.Information(AppConstants.Logging.NotificationPermissionDiagnosticsLog.PermissionGrantedAutoDismissingModalInOneSecond);
                await Task.Delay(1000);
                
                // Double-check permission status and dismissing flag before dismissing
                CheckPermissionStatus();
                
                if (IsNotificationPermissionGranted && !isDismissing && DismissCommand is AsyncRelayCommand asyncCommand)
                {
                    await asyncCommand.ExecuteAsync(null);
                }
            }
        });
    }

    private void OnPermissionDenied(object? sender, EventArgs e)
    {
        logger.Information(AppConstants.Logging.NotificationPermissionDiagnosticsLog.NotificationPermissionDeniedEventReceived);
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
            logger.Warning(ex, AppConstants.Logging.NotificationPermissionDiagnosticsLog.ErrorUpdatingHomePageButtonVisibilityAfterModalClose);
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

    /// <summary>
    /// True if the OS may still show the system permission prompt (iOS: not yet denied; Android: not permanently denied).
    /// When false, the "Request permission" button is hidden and the user must use "Open Settings".
    /// </summary>
    public bool CanShowSystemPrompt => canShowSystemPrompt;

#if ANDROID || IOS
    private void UpdateCanShowSystemPrompt(bool value)
    {
        if (SetProperty(ref canShowSystemPrompt, value))
        {
            OnPropertyChanged(nameof(IsRequestButtonVisible));
        }
    }
#endif

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Localized labels bind from XAML to this modal ViewModel.")]
    [SuppressMessage("SonarAnalyzer.CSharp", "S2325", Justification = "Same as CA1822.")]
    public string RequestButtonText => AppConstants.NotificationPermissionModalMessages.RequestNotificationPermissionButtonLabel;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Localized labels bind from XAML to this modal ViewModel.")]
    [SuppressMessage("SonarAnalyzer.CSharp", "S2325", Justification = "Same as CA1822.")]
    public string OpenSettingsButtonText
    {
        get
        {
#if ANDROID
            return AppConstants.NotificationPermissionModalMessages.OpenAppSettingsButtonLabel;
#else
            return AppConstants.NotificationPermissionModalMessages.OpenSettingsButtonLabel;
#endif
        }
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Localized labels bind from XAML to this modal ViewModel.")]
    [SuppressMessage("SonarAnalyzer.CSharp", "S2325", Justification = "Same as CA1822.")]
    public string MainMessage
    {
        get
        {
#if ANDROID
            return AppConstants.NotificationPermissionModalMessages.MainAndroidTapToPlayReminders;
#elif IOS
            return AppConstants.NotificationPermissionModalMessages.MainIosScheduledAlarms;
#else
            return AppConstants.NotificationPermissionModalMessages.MainOtherPlatforms;
#endif
        }
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Localized labels bind from XAML to this modal ViewModel.")]
    [SuppressMessage("SonarAnalyzer.CSharp", "S2325", Justification = "Same as CA1822.")]
    public string InstructionsText
    {
        get
        {
#if ANDROID
            return AppConstants.NotificationPermissionModalMessages.InstructionsAndroidAfterAllowDialog;
#elif IOS
            return AppConstants.NotificationPermissionModalMessages.InstructionsIosAfterAllowDialog;
#else
            return AppConstants.NotificationPermissionModalMessages.InstructionsOtherPlatformsEnableInSettings;
#endif
        }
    }

    /// <summary>
    /// Shows the request button only if permission is not granted and the OS can still show the system prompt.
    /// When the user has denied (iOS once, Android permanently), the button is hidden; use "Open Settings" instead.
    /// </summary>
    public bool IsRequestButtonVisible => !IsNotificationPermissionGranted && CanShowSystemPrompt;

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
