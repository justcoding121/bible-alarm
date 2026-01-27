#nullable enable

using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.Battery.Interfaces;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.ViewModels.General;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;
using Bible.Alarm.Common.Messenger;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels;

public sealed class HomeViewModel : ObservableObject, IDisposable, IRecipient<ShowProgressBarMessage>, IRecipient<HideProgressBarMessage>
{
    private readonly ILogger logger;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IServiceProvider serviceProvider;

    private readonly IDatabaseSeedService databaseSeedService;
    private readonly IScheduleMigrationService scheduleMigrationService;
    private readonly IAlarmScheduleService alarmScheduleService;

    private readonly Func<int, ScheduleListItemViewModel> scheduleListItemFactory;
    private readonly IMapper mapper;

    private readonly IDispatcher dispatcher;
    private readonly IState<ApplicationState> state;
    private readonly IState<PlaybackState> playbackState;
    private readonly INavigationService navigationService;

    // Helper classes
    private readonly ScheduleDataPreparer scheduleDataPreparer;
    private readonly ScheduleViewModelManager scheduleViewModelManager;
    private readonly HomeNavigationHelper navigationHelper;
    private readonly ProgressBarAnimator progressAnimator;
    private readonly ProgressBarManager progressBarManager;
    private readonly PropertyManager propertyManager;
    private readonly CommandHandler commandHandler;
    private readonly HomeStateChangeHandler stateChangeHandler;
    private readonly BootstrapReadyManager bootstrapReadyManager;

    public HomeViewModel(
        ILogger logger,
        IServiceScopeFactory scopeFactory,
        IServiceProvider serviceProvider,
        Func<int, ScheduleListItemViewModel> scheduleListItemFactory,
        IState<ApplicationState> state,
        IState<PlaybackState> playbackState,
        IDispatcher dispatcher,
        IDatabaseSeedService databaseSeedService,
        IScheduleMigrationService scheduleMigrationService,
        INavigationService navigationService,
        IAlarmScheduleService alarmScheduleService,
        IMapper mapper)
    {
        this.logger = logger;
        this.scopeFactory = scopeFactory;
        this.serviceProvider = serviceProvider;
        this.scheduleListItemFactory = scheduleListItemFactory;
        this.state = state;
        this.playbackState = playbackState;
        this.dispatcher = dispatcher;
        this.databaseSeedService = databaseSeedService;
        this.scheduleMigrationService = scheduleMigrationService;
        this.alarmScheduleService = alarmScheduleService;
        this.navigationService = navigationService;
        this.mapper = mapper;

        // Initialize helper classes
        scheduleDataPreparer = new ScheduleDataPreparer(mapper);
        navigationHelper = new HomeNavigationHelper(logger, dispatcher, navigationService, playbackState);
        scheduleViewModelManager = new ScheduleViewModelManager(logger, serviceProvider, navigationHelper.TrackPlayClick);
        progressAnimator = new ProgressBarAnimator();
        progressBarManager = new ProgressBarManager(progressAnimator);
        propertyManager = new PropertyManager();
        bootstrapReadyManager = new BootstrapReadyManager(logger);

        // Setup progress bar manager events
        progressBarManager.ProgressBarOpacityChanged += (opacity) =>
        {
            OnPropertyChanged(nameof(ProgressBarOpacity));
        };
        progressBarManager.ProgressBarHiddenChanged += () =>
        {
            OnPropertyChanged(nameof(IsProgressBarHidden));
        };
        // Note: Animation is now handled natively in AnimatedProgressBar control
        // No need for ProgressAnimator property change notifications

        // Setup property manager events
        propertyManager.SchedulesChanged += () =>
        {
            OnPropertyChanged(nameof(Schedules));
            progressBarManager.UpdateVisibility(propertyManager.IsBusy, propertyManager.Schedules?.Count);
            // Check if schedules are loaded (indicates bootstrap is complete)
            bootstrapReadyManager.CheckSchedulesLoaded(propertyManager.Schedules);
        };
        propertyManager.IsBusyChanged += (isBusy) =>
        {
            OnPropertyChanged(nameof(IsBusy));
            progressBarManager.UpdateVisibility(isBusy, propertyManager.Schedules?.Count);
        };
        propertyManager.LoadedChanged += (loaded) =>
        {
            OnPropertyChanged(nameof(Loaded));
            // Check bootstrap when loaded changes
            bootstrapReadyManager.UpdateBootstrapReadyState();
        };
        propertyManager.IsAddBusyChanged += (isAddBusy) =>
        {
            OnPropertyChanged(nameof(IsAddBusy));
        };

        // Setup bootstrap ready manager events
        bootstrapReadyManager.BootstrapReadyChanged += (isReady) =>
        {
            OnPropertyChanged(nameof(IsBootstrapReady));
        };

        // Initialize command handler
        commandHandler = new CommandHandler(
            dispatcher,
            navigationService,
            (x) => x.Schedule?.Id > 0 && navigationHelper.ShouldSkipNavigation(x.Schedule.Id),
            async (x) => await navigationHelper.ShowOverlayAndNavigateAsync(x));

        AddScheduleCommand = commandHandler.CreateAddScheduleCommand((isBusy) => propertyManager.IsAddBusy = isBusy);
        ViewScheduleCommand = commandHandler.CreateViewScheduleCommand(
            () => progressBarManager.ShowTemporarily(),
            async () => await progressBarManager.HideTemporarilyAsync());

        // Command to open alarm settings modal (Android only)
        OpenAlarmSettingsCommand = new AsyncRelayCommand(async () =>
        {
            if (DeviceInfo.Platform != DevicePlatform.Android)
            {
                return;
            }

            try
            {
                var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
                if (batteryService == null)
                {
                    logger.Warning("IBatteryOptimizationService not available");
                    return;
                }

                // Create a view model for the battery optimization modal
                var batteryViewModel = new BatteryOptimizationViewModel(
                    logger,
                    navigationService,
                    serviceProvider);

                if (batteryService.CanShowOptimizeActivity())
                {
                    batteryViewModel.CanOptimizeBattery = true;
                }

                // Start permission check timer when opening battery optimization modal
                batteryViewModel.StartPermissionCheckTimer();
                await navigationService.OpenBatteryOptimizationModalAsync(batteryViewModel);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error opening alarm settings modal from floating button");
            }
        });

        // Initialize state change handler
        stateChangeHandler = new HomeStateChangeHandler(
            logger,
            scheduleDataPreparer,
            scheduleViewModelManager,
            progressAnimator,
            (isBusy) => propertyManager.IsBusy = isBusy,
            (opacity) => progressBarManager.ProgressBarOpacity = opacity,
            () => propertyManager.IsBusy,
            () => propertyManager.Schedules,
            (schedules) => propertyManager.Schedules = schedules,
            () => progressBarManager.UpdateVisibility(propertyManager.IsBusy, propertyManager.Schedules?.Count),
            async () => await progressBarManager.FadeOutAsync());

        state.StateChanged += OnStateChanged;

        // Immediately process current state when ViewModel is created
        OnStateChanged(this, EventArgs.Empty);

        // Ensure progress bar visibility is updated on initial load
        progressBarManager.UpdateVisibility(propertyManager.IsBusy, propertyManager.Schedules?.Count);

        // Start tracking bootstrap ready state
        bootstrapReadyManager.StartTracking();

        // Register for progress bar messages
        WeakReferenceMessenger.Default.Register<ShowProgressBarMessage>(this);
        WeakReferenceMessenger.Default.Register<HideProgressBarMessage>(this);
    }


    public ObservableHashSet<ScheduleListItemViewModel> Schedules
    {
        get => propertyManager.Schedules;
        set => propertyManager.Schedules = value;
    }

    public bool IsBusy
    {
        get => propertyManager.IsBusy;
        set => propertyManager.IsBusy = value;
    }

    public bool Loaded
    {
        get => propertyManager.Loaded;
        set => propertyManager.Loaded = value;
    }


    public bool IsLoadingSchedules
    {
        get
        {
            var isLoading = progressBarManager.ProgressBarOpacity > 0 && (IsBusy || (Schedules == null || Schedules.Count == 0));
            return isLoading;
        }
    }

    public double ProgressBarOpacity => progressBarManager.ProgressBarOpacity;
    public bool IsProgressBarHidden => progressBarManager.IsProgressBarHidden;
    public double AnimatedProgressStart => progressBarManager.AnimatedProgressStart;
    public double AnimatedProgressEnd => progressBarManager.AnimatedProgressEnd;
    public double AnimatedProgress => progressBarManager.AnimatedProgress;
    public double AnimatedProgressRangeWidth => progressBarManager.AnimatedProgressRangeWidth;

    /// <summary>
    /// Indicates if bootstrap is complete and databases are ready.
    /// Available for debugging/logging purposes.
    /// </summary>
    public bool IsBootstrapComplete => Common.Helpers.BootstrapHelper.IsBootstrapCompleted();

    /// <summary>
    /// Indicates if bootstrap is complete and the add button should be enabled.
    /// This property updates automatically when bootstrap completes.
    /// </summary>
    public bool IsBootstrapReady => bootstrapReadyManager.IsBootstrapReady;

    public bool IsAddBusy
    {
        get => propertyManager.IsAddBusy;
        set => propertyManager.IsAddBusy = value;
    }

    /// <summary>
    /// Hides the Schedule page overlay. Called when navigating back to Home page.
    /// </summary>
    public void HideSchedulePageOverlay() => dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });

    /// <summary>
    /// Resets all schedule-related state when navigating back to home.
    /// This ensures only one schedule is in state at any time.
    /// </summary>
    public void ResetScheduleState() => dispatcher.Dispatch(new ResetScheduleStateAction());

    /// <summary>
    /// Updates the floating button visibility based on permission status.
    /// Hides button if both battery optimization and DND permissions are granted.
    /// </summary>
    public void UpdateFloatingButtonVisibility()
    {
        if (DeviceInfo.Platform != DevicePlatform.Android)
        {
            IsFloatingButtonVisible = false;
            CollectionViewBottomMargin = 0;
            return;
        }

        try
        {
            var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
            if (batteryService != null)
            {
                var isBatteryExcluded = batteryService.IsIgnoringBatteryOptimizations();
                var isDndGranted = batteryService.IsNotificationPolicyAccessGranted();

                // Hide button if both permissions are granted
                var shouldShow = !(isBatteryExcluded && isDndGranted);

                if (IsFloatingButtonVisible != shouldShow)
                {
                    IsFloatingButtonVisible = shouldShow;
                    CollectionViewBottomMargin = shouldShow ? 80 : 0; // 56 (button) + 24 (margin)
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating floating button visibility");
            // Default to showing button if there's an error
            IsFloatingButtonVisible = true;
            CollectionViewBottomMargin = 80;
        }
    }

    /// <summary>
    /// Checks if the alarm settings modal should be shown on first app launch.
    /// This is called once when the home page loads for the first time.
    /// </summary>
    public async Task CheckAndShowAlarmSettingsOnFirstLaunchAsync()
    {
        if (DeviceInfo.Platform != DevicePlatform.Android)
        {
            return;
        }

        try
        {
            var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
            if (batteryService == null)
            {
                return;
            }

            // Check if this is the first launch and modal hasn't been shown yet
            // Uses the same database check as ShouldShowModalAsync (saved to GeneralSettings table)
            if (await batteryService.ShouldShowModalAsync())
            {
                // Create a view model for the battery optimization modal
                var batteryViewModel = new BatteryOptimizationViewModel(
                    logger,
                    navigationService,
                    serviceProvider);

                if (batteryService.CanShowOptimizeActivity())
                {
                    batteryViewModel.CanOptimizeBattery = true;
                }

                // Start permission check timer when opening battery optimization modal
                batteryViewModel.StartPermissionCheckTimer();
                await navigationService.OpenBatteryOptimizationModalAsync(batteryViewModel);
            }

            // Update floating button visibility after checking permissions
            UpdateFloatingButtonVisibility();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking and showing alarm settings on first launch");
        }
    }


    public ICommand AddScheduleCommand { get; set; }
    public ICommand ViewScheduleCommand { get; set; }
    public ICommand OpenAlarmSettingsCommand { get; private set; } = null!;

    private bool isFloatingButtonVisible = true;
    public bool IsFloatingButtonVisible
    {
        get => isFloatingButtonVisible;
        set => SetProperty(ref isFloatingButtonVisible, value);
    }

    private double collectionViewBottomMargin = 0;
    public double CollectionViewBottomMargin
    {
        get => collectionViewBottomMargin;
        set => SetProperty(ref collectionViewBottomMargin, value);
    }


    private void OnStateChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await stateChangeHandler.HandleStateChangedAsync(stateValue);
        });
    }

    public void Receive(ShowProgressBarMessage message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Show progress bar temporarily (doesn't affect normal visibility logic)
            progressBarManager.ShowTemporarily();
        });
    }

    public void Receive(HideProgressBarMessage message)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Hide progress bar temporarily (doesn't affect normal visibility logic)
            await progressBarManager.HideTemporarilyAsync();
        });
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
        WeakReferenceMessenger.Default.Unregister<ShowProgressBarMessage>(this);
        WeakReferenceMessenger.Default.Unregister<HideProgressBarMessage>(this);
        bootstrapReadyManager.Dispose();
        progressBarManager.Dispose();
        scheduleViewModelManager.DisposeAll();
    }
}
