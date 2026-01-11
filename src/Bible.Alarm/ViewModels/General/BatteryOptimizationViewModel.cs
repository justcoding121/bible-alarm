#nullable enable

using System.Windows.Input;
using Bible.Alarm.Services.Battery.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Bible.Alarm.ViewModels.General;

public sealed class BatteryOptimizationViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
    private readonly IServiceProvider serviceProvider;

    private bool canOptimizeBattery;
    private bool isBatteryOptimizationExcluded;
    private bool isDndAccessGranted;
    private System.Timers.Timer? permissionCheckTimer;

    public BatteryOptimizationViewModel(
        ILogger logger,
        INavigationService navigationService,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.navigationService = navigationService;
        this.serviceProvider = serviceProvider;

        InitializeCommands();
        InitializePermissionStatus();
    }

    private void InitializeCommands()
    {
        BatteryOptimizationExcludeCommand = new AsyncRelayCommand(async () =>
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
                if (batteryService != null)
                {
                    // Don't dismiss modal - keep it open so user can see status update when they return
                    batteryService.ShowOptimizationSettingsPage();
                }
            }
        });

        BatteryOptimizationDismissCommand = new AsyncRelayCommand(async () =>
        {
            StopPermissionCheckTimer();
            await MarkBatteryOptimizationModalAsShown();
            await navigationService.PopModalAsync();
            UpdateHomePageButtonVisibility();
        });

        DoNotDisturbExcludeCommand = new AsyncRelayCommand(async () =>
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
                if (batteryService != null)
                {
                    // Don't dismiss modal - keep it open so user can see status update when they return
                    batteryService.ShowDoNotDisturbSettingsPage();
                }
            }
        });
    }

    private void InitializePermissionStatus()
    {
        if (DeviceInfo.Platform != DevicePlatform.Android)
        {
            return;
        }

        CheckPermissionStatus();
    }

    private void CheckPermissionStatus()
    {
        if (DeviceInfo.Platform != DevicePlatform.Android)
        {
            return;
        }

        try
        {
            logger.Debug("Checking permission status...");
            var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
            if (batteryService != null)
            {
                var wasBatteryExcluded = IsBatteryOptimizationExcluded;
                var wasDndGranted = IsDndAccessGranted;

                IsBatteryOptimizationExcluded = batteryService.IsIgnoringBatteryOptimizations();
                IsDndAccessGranted = batteryService.IsNotificationPolicyAccessGranted();

                logger.Debug("Permission check completed - Battery Excluded: {IsExcluded} (was {WasExcluded}), DND Granted: {IsGranted} (was {WasGranted})",
                    IsBatteryOptimizationExcluded, wasBatteryExcluded, IsDndAccessGranted, wasDndGranted);
            }
            else
            {
                logger.Warning("IBatteryOptimizationService is null, cannot check permissions");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking permission status");
        }
    }

    public void StartPermissionCheckTimer()
    {
        if (DeviceInfo.Platform != DevicePlatform.Android)
        {
            return;
        }

        // Stop any existing timer before starting a new one
        StopPermissionCheckTimer();

        logger.Debug("Starting permission check timer for battery optimization modal");

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

    private void StopPermissionCheckTimer()
    {
        if (permissionCheckTimer != null)
        {
            permissionCheckTimer.Stop();
            permissionCheckTimer.Dispose();
            permissionCheckTimer = null;
        }
    }

    private void UpdateHomePageButtonVisibility()
    {
        try
        {
            if (DeviceInfo.Platform != DevicePlatform.Android)
            {
                return;
            }

            // Get the Home page from navigation service
            var homePage = navigationService.GetCurrentHomePage();
            if (homePage?.BindingContext is HomeViewModel homeViewModel)
            {
                // Update the floating button visibility based on current permissions
                homeViewModel.UpdateFloatingButtonVisibility();
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error updating home page button visibility after modal close");
        }
    }

    private async Task MarkBatteryOptimizationModalAsShown()
    {
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
            if (batteryService != null)
            {
                await batteryService.MarkModalAsShownAsync();
            }
        }
    }

    public bool CanOptimizeBattery
    {
        get => canOptimizeBattery;
        set
        {
            if (SetProperty(ref canOptimizeBattery, value))
            {
                OnPropertyChanged(nameof(IsBatteryOptimizationButtonVisible));
                OnPropertyChanged(nameof(IsDndButtonVisible));
                OnPropertyChanged(nameof(IsInstructionsVisible));
            }
        }
    }

    public bool IsBatteryOptimizationExcluded
    {
        get => isBatteryOptimizationExcluded;
        set
        {
            if (SetProperty(ref isBatteryOptimizationExcluded, value))
            {
                OnPropertyChanged(nameof(IsBatteryOptimizationButtonVisible));
                OnPropertyChanged(nameof(IsInstructionsVisible));
            }
        }
    }

    public bool IsDndAccessGranted
    {
        get => isDndAccessGranted;
        set
        {
            if (SetProperty(ref isDndAccessGranted, value))
            {
                OnPropertyChanged(nameof(IsDndButtonVisible));
                OnPropertyChanged(nameof(IsInstructionsVisible));
            }
        }
    }

    public string BatteryOptimizationButtonText => "OPEN BATTERY SETTINGS";

    public string DndButtonText => "OPEN DND SETTINGS";

    /// <summary>
    /// Shows the battery optimization button only if battery optimization is not excluded and the feature is available.
    /// </summary>
    public bool IsBatteryOptimizationButtonVisible => CanOptimizeBattery && !IsBatteryOptimizationExcluded;

    /// <summary>
    /// Shows the DND button only if DND access is not granted and battery optimization feature is available.
    /// </summary>
    public bool IsDndButtonVisible => CanOptimizeBattery && !IsDndAccessGranted;

    /// <summary>
    /// Shows the instructions label only if at least one button is visible (i.e., there's something to configure).
    /// </summary>
    public bool IsInstructionsVisible => IsBatteryOptimizationButtonVisible || IsDndButtonVisible;

    public ICommand BatteryOptimizationExcludeCommand { get; private set; } = null!;
    public ICommand BatteryOptimizationDismissCommand { get; private set; } = null!;
    public ICommand DoNotDisturbExcludeCommand { get; private set; } = null!;

    public void Dispose()
    {
        StopPermissionCheckTimer();
    }
}
