#nullable enable

using System.Windows.Input;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Platforms.iOS.Services.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using Bible.Alarm.ViewModels;

namespace Bible.Alarm.ViewModels.General;

public sealed class IOSNotificationPermissionViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
    private readonly IOSNotificationPermissionService permissionService;

    private bool isNotificationPermissionGranted;
    private System.Timers.Timer? permissionCheckTimer;

    public IOSNotificationPermissionViewModel(
        ILogger logger,
        INavigationService navigationService)
    {
        this.logger = logger;
        this.navigationService = navigationService;
        permissionService = IOSNotificationPermissionService.Instance;

        InitializeCommands();
        InitializePermissionStatus();
    }

    private void InitializeCommands()
    {
        RequestNotificationPermissionCommand = new AsyncRelayCommand(async () =>
        {
            if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
                // Request permission - will fire PermissionGranted or PermissionDenied event
                permissionService.RequestPermissionIfNeeded();
            }
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
        if (DeviceInfo.Platform != DevicePlatform.iOS)
        {
            return;
        }

        CheckPermissionStatus();
    }

    private void CheckPermissionStatus()
    {
        if (DeviceInfo.Platform != DevicePlatform.iOS)
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

    public void StartPermissionCheckTimer()
    {
        if (DeviceInfo.Platform != DevicePlatform.iOS)
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
    }

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

    private void StopPermissionCheckTimer()
    {
        if (permissionCheckTimer != null)
        {
            permissionCheckTimer.Stop();
            permissionCheckTimer.Dispose();
            permissionCheckTimer = null;
        }

        // Unsubscribe from permission events
        permissionService.PermissionGranted -= OnPermissionGranted;
        permissionService.PermissionDenied -= OnPermissionDenied;
    }

    private void UpdateHomePageButtonVisibility()
    {
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
