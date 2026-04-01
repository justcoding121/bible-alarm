#nullable enable

using System.Linq;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
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
    private readonly HomeViewModelNotificationPermissionHandler notificationPermissionHandler;
    private readonly HomeViewModelFloatingButtonHandler floatingButtonHandler;

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
        var playbackModalService = serviceProvider.GetRequiredService<IPlaybackModalService>();
        navigationHelper = new HomeNavigationHelper(logger, dispatcher, navigationService, playbackModalService, playbackState, serviceProvider, mapper);
        scheduleViewModelManager = new ScheduleViewModelManager(logger, serviceProvider, navigationHelper.TrackPlayClick);
        progressAnimator = new ProgressBarAnimator();
        progressBarManager = new ProgressBarManager(progressAnimator);
        propertyManager = new PropertyManager();
        bootstrapReadyManager = new BootstrapReadyManager(logger);
        notificationPermissionHandler = new HomeViewModelNotificationPermissionHandler(logger, state);
        floatingButtonHandler = new HomeViewModelFloatingButtonHandler(logger, serviceProvider);

        progressBarManager.ProgressBarOpacityChanged += OnProgressBarOpacityChanged;
        progressBarManager.ProgressBarHiddenChanged += OnProgressBarHiddenChanged;
        propertyManager.SchedulesChanged += OnSchedulesChanged;
        propertyManager.IsBusyChanged += OnIsBusyChanged;
        propertyManager.LoadedChanged += OnLoadedChanged;
        propertyManager.IsAddBusyChanged += OnIsAddBusyChanged;
        bootstrapReadyManager.BootstrapReadyChanged += OnBootstrapReadyChanged;

        commandHandler = new CommandHandler(
            logger,
            dispatcher,
            navigationService,
            serviceProvider,
            (x) => x.Schedule?.Id > 0 && navigationHelper.ShouldSkipNavigation(x.Schedule.Id),
            async (x) => await navigationHelper.ShowOverlayAndNavigateAsync(x));

        AddScheduleCommand = commandHandler.CreateAddScheduleCommand(
            (isBusy) => propertyManager.IsAddBusy = isBusy,
            () => bootstrapReadyManager.IsBootstrapReady);
        ViewScheduleCommand = commandHandler.CreateViewScheduleCommand(
            () => progressBarManager.ShowTemporarily(),
            async () => await progressBarManager.HideTemporarilyAsync());
        OpenAlarmSettingsCommand = commandHandler.CreateOpenAlarmSettingsCommand();
        OpenNotificationPermissionCommand = commandHandler.CreateOpenNotificationPermissionCommand();

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
            () =>
            {
                OnPropertyChanged(nameof(Schedules));
                progressBarManager.UpdateVisibility(propertyManager.IsBusy, propertyManager.Schedules?.Count);
                bootstrapReadyManager.CheckSchedulesLoaded(propertyManager.Schedules);
            },
            () => progressBarManager.UpdateVisibility(propertyManager.IsBusy, propertyManager.Schedules?.Count),
            async () => await progressBarManager.FadeOutAsync(),
            () => navigationService.IsPlaybackModalOnScreen() || playbackModalService.IsModalOpenOrPending);

        state.StateChanged += OnStateChanged;
        playbackState.StateChanged += OnPlaybackStateChanged;

        // Immediately process current state when ViewModel is created
        OnStateChanged(this, EventArgs.Empty);

        // Ensure progress bar visibility is updated on initial load
        progressBarManager.UpdateVisibility(propertyManager.IsBusy, propertyManager.Schedules?.Count);

        bootstrapReadyManager.StartTracking();
        bootstrapReadyManager.BootstrapReadyChanged += OnBootstrapReadyChangedWithNotificationUpdate;

        // Register for progress bar messages
        WeakReferenceMessenger.Default.Register<ShowProgressBarMessage>(this);
        WeakReferenceMessenger.Default.Register<HideProgressBarMessage>(this);

        // When the playback modal is confirmed on screen the home list is hidden behind it —
        // apply any deferred reorder now so the list is in the right order before it reappears.
        WeakReferenceMessenger.Default.Register<PlaybackModalOpenedMessage>(this, (r, m) =>
            _ = stateChangeHandler.ApplyDeferredReorderAsync());
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
    /// Updates the battery optimization floating button visibility (Android only).
    /// </summary>
    public void UpdateFloatingButtonVisibility()
    {
        var shouldShow = floatingButtonHandler.ComputeFloatingButtonVisible();
        if (IsFloatingButtonVisible != shouldShow)
        {
            IsFloatingButtonVisible = shouldShow;
        }
        logger.Information("[FLOATING-BUTTON] Calling UpdateNotificationPermissionButtonVisibility");
        UpdateNotificationPermissionButtonVisibility();
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            OnPropertyChanged(nameof(NotificationPermissionButtonMargin));
        }
    }

    /// <summary>
    /// Checks alarm settings permissions and updates UI visibility on first app launch.
    /// This is called once when the home page loads for the first time.
    /// The modal is no longer shown automatically - users must tap the warning icon button to open it.
    /// </summary>
    public async Task CheckAndShowAlarmSettingsOnFirstLaunchAsync()
    {
        try
        {
            logger.Information("[NOTIFICATION-BUTTON] CheckAndShowAlarmSettingsOnFirstLaunchAsync called");
            
            // Update button visibility based on current permission status
            // The modal will only be shown when user taps the warning icon button
            UpdateFloatingButtonVisibility();
            // Also explicitly check notification permission button
            UpdateNotificationPermissionButtonVisibility();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking alarm settings on first launch");
        }
    }


    public ICommand AddScheduleCommand { get; set; }
    public ICommand ViewScheduleCommand { get; set; }
    public ICommand OpenAlarmSettingsCommand { get; private set; } = null!;
    public ICommand OpenNotificationPermissionCommand { get; private set; } = null!;

    private bool isFloatingButtonVisible = true;
    public bool IsFloatingButtonVisible
    {
        get => isFloatingButtonVisible;
        set
        {
            if (SetProperty(ref isFloatingButtonVisible, value))
            {
                // Update notification button margin when battery button visibility changes (Android)
                if (DeviceInfo.Platform == DevicePlatform.Android)
                {
                    logger.Debug("[MARGIN] Battery button visibility changed to {Visible}, recalculating notification margin", value);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        OnPropertyChanged(nameof(NotificationPermissionButtonMargin));
                    });
                }
            }
        }
    }

    private bool isNotificationPermissionButtonVisible = false;
    public bool IsNotificationPermissionButtonVisible
    {
        get => isNotificationPermissionButtonVisible;
        set
        {
            if (SetProperty(ref isNotificationPermissionButtonVisible, value))
            {
                // Force UI update
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotificationPermissionButtonVisible));
                // Update margin when visibility changes to ensure correct bottom position
                logger.Debug("[MARGIN] Notification button visibility changed to {Visible}, recalculating margin", value);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnPropertyChanged(nameof(NotificationPermissionButtonMargin));
                });
            }
        }
    }

    private double collectionViewBottomMargin = 0;
    public double CollectionViewBottomMargin
    {
        get => collectionViewBottomMargin;
        set => SetProperty(ref collectionViewBottomMargin, value);
    }

    private double notificationPermissionButtonBottomMargin = 0;
    public double NotificationPermissionButtonBottomMargin
    {
        get => notificationPermissionButtonBottomMargin;
        set
        {
            if (SetProperty(ref notificationPermissionButtonBottomMargin, value))
            {
                OnPropertyChanged(nameof(NotificationPermissionButtonMargin));
            }
        }
    }

    public Thickness NotificationPermissionButtonMargin
    {
        get
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                // Android: Left bottom corner (24px from left, 24px from bottom)
                // Bottom margin matches battery button (24px) for alignment
                var bottomMargin = IsNotificationPermissionButtonVisible ? 24 : 0;
                var margin = new Thickness(24, 0, 0, bottomMargin);
                logger.Debug("[MARGIN] Notification button margin: {Margin} (bottom: {Bottom}px, matches battery button: 24px)", margin, bottomMargin);
                return margin;
            }
            else if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
                // iOS: Right bottom corner (24px from right, 24px from bottom)
                return new Thickness(0, 0, 24, IsNotificationPermissionButtonVisible ? 24 : 0);
            }
            
            // Other platforms (WinUI): use left bottom corner
            return new Thickness(24, 0, 0, IsNotificationPermissionButtonVisible ? 24 : 0);
        }
    }



    private void OnProgressBarOpacityChanged(double _) => OnPropertyChanged(nameof(ProgressBarOpacity));
    private void OnProgressBarHiddenChanged() => OnPropertyChanged(nameof(IsProgressBarHidden));
    private void OnSchedulesChanged()
    {
        OnPropertyChanged(nameof(Schedules));
        progressBarManager.UpdateVisibility(propertyManager.IsBusy, propertyManager.Schedules?.Count);
        bootstrapReadyManager.CheckSchedulesLoaded(propertyManager.Schedules);
    }
    private void OnIsBusyChanged(bool _)
    {
        OnPropertyChanged(nameof(IsBusy));
        progressBarManager.UpdateVisibility(propertyManager.IsBusy, propertyManager.Schedules?.Count);
    }
    private void OnLoadedChanged(bool _)
    {
        OnPropertyChanged(nameof(Loaded));
        bootstrapReadyManager.UpdateBootstrapReadyState();
    }
    private void OnIsAddBusyChanged(bool _) => OnPropertyChanged(nameof(IsAddBusy));
    private void OnBootstrapReadyChanged(bool _) => OnPropertyChanged(nameof(IsBootstrapReady));
    private void OnBootstrapReadyChangedWithNotificationUpdate(bool isReady)
    {
        logger.Information("[BOOTSTRAP-READY] BootstrapReadyChanged event fired - isReady={IsReady}", isReady);
        if (isReady)
        {
            logger.Information("[BOOTSTRAP-READY] Bootstrap ready, calling UpdateNotificationPermissionButtonVisibility");
            UpdateNotificationPermissionButtonVisibility();
            logger.Information("[BOOTSTRAP-READY] UpdateNotificationPermissionButtonVisibility completed");
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        try
        {
            var stateValue = state.Value;
            var scheduleCount = stateValue.Schedules?.Count ?? 0;

            logger.Debug("[STATE-CHANGE] OnStateChanged called - ScheduleCount={Count}", scheduleCount);
            logger.Information("[STATE-CHANGE] OnStateChanged called - ScheduleCount={Count}", scheduleCount);
            
            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    logger.Debug("[STATE-CHANGE] Inside async lambda, calling HandleStateChangedAsync");
                    await stateChangeHandler.HandleStateChangedAsync(stateValue);
                    logger.Information("[STATE-CHANGE] HandleStateChangedAsync completed, calling UpdateFloatingButtonVisibility");
                    // Update button visibility when schedules change (e.g., NotificationEnabled or IsEnabled changes)
                    UpdateFloatingButtonVisibility();
                    logger.Information("[STATE-CHANGE] UpdateFloatingButtonVisibility completed");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "[STATE-CHANGE] Error in OnStateChanged async handler");
                }
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[STATE-CHANGE] Error at start of OnStateChanged");
        }
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        // Refresh list so the playing schedule row shows PlaybackState.Title instead of saved track
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


    /// <summary>
    /// Updates the notification permission button visibility based on permission status and schedules.
    /// </summary>
    public void UpdateNotificationPermissionButtonVisibility() =>
        notificationPermissionHandler.UpdateVisibility(
            v => IsNotificationPermissionButtonVisible = v,
            v => NotificationPermissionButtonBottomMargin = v,
            v => CollectionViewBottomMargin = v,
            () => IsFloatingButtonVisible,
            () => IsNotificationPermissionButtonVisible,
            () => OnPropertyChanged(nameof(NotificationPermissionButtonMargin)));

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
        playbackState.StateChanged -= OnPlaybackStateChanged;
        WeakReferenceMessenger.Default.Unregister<ShowProgressBarMessage>(this);
        WeakReferenceMessenger.Default.Unregister<HideProgressBarMessage>(this);
        WeakReferenceMessenger.Default.Unregister<PlaybackModalOpenedMessage>(this);

        progressBarManager.ProgressBarOpacityChanged -= OnProgressBarOpacityChanged;
        progressBarManager.ProgressBarHiddenChanged -= OnProgressBarHiddenChanged;
        propertyManager.SchedulesChanged -= OnSchedulesChanged;
        propertyManager.IsBusyChanged -= OnIsBusyChanged;
        propertyManager.LoadedChanged -= OnLoadedChanged;
        propertyManager.IsAddBusyChanged -= OnIsAddBusyChanged;
        bootstrapReadyManager.BootstrapReadyChanged -= OnBootstrapReadyChanged;
        bootstrapReadyManager.BootstrapReadyChanged -= OnBootstrapReadyChangedWithNotificationUpdate;

        bootstrapReadyManager.Dispose();
        progressBarManager.Dispose();
        progressAnimator.Dispose();
        scheduleViewModelManager.DisposeAll();
    }
}
