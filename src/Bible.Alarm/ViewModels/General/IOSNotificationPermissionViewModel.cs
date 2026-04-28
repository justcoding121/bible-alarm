#nullable enable

using System.Windows.Input;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

#if IOS
using Bible.Alarm.Platforms.iOS.Services.Helpers;
#endif

namespace Bible.Alarm.ViewModels.General;

public sealed class IOSNotificationPermissionViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
#if IOS
    private readonly IOSNotificationPermissionService permissionService;
#else
#pragma warning disable CS0649, CS0169 // Field is never assigned/used on non-iOS platforms
    private readonly object? permissionService;
#pragma warning restore CS0649, CS0169
#endif

    private bool isNotificationPermissionGranted;
    private System.Timers.Timer? permissionCheckTimer;

    public IOSNotificationPermissionViewModel(
        ILogger logger,
        INavigationService navigationService)
    {
        this.logger = logger;
        this.navigationService = navigationService;
#if IOS
        permissionService = IOSNotificationPermissionService.Instance;
#endif

        InitializeCommands();
        InitializePermissionStatus();
    }

    private void InitializeCommands()
    {
        RequestNotificationPermissionCommand = new AsyncRelayCommand(async () =>
        {
#if IOS
            if (DeviceInfo.Platform == DevicePlatform.iOS && permissionService != null)
            {
                // Request permission - will fire PermissionGranted or PermissionDenied event
                permissionService.RequestPermissionIfNeeded();
            }
#endif
        });

        DismissCommand = new AsyncRelayCommand(async () =>
        {
            StopPermissionCheckTimer();
            await navigationService.PopModalAsync();
            UpdateHomePageButtonVisibility();
        });
    }

    private void InitializePermissionStatus()
    {
#if IOS
        if (DeviceInfo.Platform != DevicePlatform.iOS)
        {
            return;
        }

        CheckPermissionStatus();
#endif
    }

#if IOS
    private void CheckPermissionStatus()
    {
        if (DeviceInfo.Platform != DevicePlatform.iOS || permissionService == null)
        {
            return;
        }

        try
        {
            logger.Debug("Checking iOS notification permission status...");
            var wasGranted = IsNotificationPermissionGranted;

            IsNotificationPermissionGranted = permissionService.IsGranted;

            logger.Debug("Permission check completed - Granted: {IsGranted} (was {WasGranted})",
                IsNotificationPermissionGranted, wasGranted);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking iOS notification permission status");
        }
    }
#endif

    public void StartPermissionCheckTimer()
    {
#if IOS
        if (DeviceInfo.Platform != DevicePlatform.iOS || permissionService == null)
        {
            return;
        }

        // Stop any existing timer before starting a new one
        StopPermissionCheckTimer();

        logger.Debug("Starting permission check timer for iOS notification permission modal");

        // Subscribe to permission events
        permissionService.PermissionGranted += OnPermissionGranted;
        permissionService.PermissionDenied += OnPermissionDenied;

        // Check permissions every 1 second while modal is open
        permissionCheckTimer = new System.Timers.Timer(1000);
        permissionCheckTimer.Elapsed += (sender, e) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                logger.Debug("Permission check timer elapsed - checking permissions");
                CheckPermissionStatus();
            });
        };
        permissionCheckTimer.AutoReset = true;
        permissionCheckTimer.Start();
        logger.Debug("Permission check timer started successfully");
#endif
    }

#if IOS
    private void OnPermissionGranted(object? sender, EventArgs e)
    {
        logger.Information("iOS notification permission granted event received");
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CheckPermissionStatus();
        });
    }

    private void OnPermissionDenied(object? sender, EventArgs e)
    {
        logger.Information("iOS notification permission denied event received");
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CheckPermissionStatus();
        });
    }
#endif

    private void StopPermissionCheckTimer()
    {
        if (permissionCheckTimer != null)
        {
            permissionCheckTimer.Stop();
            permissionCheckTimer.Dispose();
            permissionCheckTimer = null;
        }

#if IOS
        // Unsubscribe from permission events
        if (permissionService != null)
        {
            permissionService.PermissionGranted -= OnPermissionGranted;
            permissionService.PermissionDenied -= OnPermissionDenied;
        }
#endif
    }

    private void UpdateHomePageButtonVisibility()
    {
#if IOS
        try
        {
            if (DeviceInfo.Platform != DevicePlatform.iOS)
            {
                return;
            }

            // Get the Home page from navigation service
            var homePage = navigationService.GetCurrentHomePage();
            if (homePage?.BindingContext is HomeViewModel homeViewModel)
            {
                // Update the notification permission button visibility based on current permissions
                // This follows the same pattern as NotificationPermissionViewModel.UpdateHomePageButtonVisibility()
                homeViewModel.UpdateNotificationPermissionButtonVisibility();
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error updating home page button visibility after modal close");
        }
#endif
    }

    public bool IsNotificationPermissionGranted
    {
        get => isNotificationPermissionGranted;
        set
        {
            if (SetProperty(ref isNotificationPermissionGranted, value))
            {
                OnPropertyChanged(nameof(IsRequestButtonVisible));
                OnPropertyChanged(nameof(IsInstructionsVisible));
            }
        }
    }

    public string RequestButtonText => "REQUEST NOTIFICATION PERMISSION";

    /// <summary>
    /// Shows the request button only if notification permission is not granted.
    /// </summary>
    public bool IsRequestButtonVisible => !IsNotificationPermissionGranted;

    /// <summary>
    /// Shows the instructions label only if permission is not granted (i.e., there's something to configure).
    /// </summary>
    public bool IsInstructionsVisible => !IsNotificationPermissionGranted;

    public ICommand RequestNotificationPermissionCommand { get; private set; } = null!;
    public ICommand DismissCommand { get; private set; } = null!;

    public void Dispose()
    {
        StopPermissionCheckTimer();
    }
}
