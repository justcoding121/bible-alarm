#nullable enable

using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.General;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
#if ANDROID
using Bible.Alarm.Platforms.Android.Services.Helpers;
#elif IOS
using Bible.Alarm.Platforms.iOS.Services.Helpers;
#endif

namespace Bible.Alarm.ViewModels.Schedule;

public sealed class NumberOfTrackContainerViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
    private readonly IServiceProvider serviceProvider;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;

    private int scheduleId;
    private bool notificationEnabled;
    private bool alwaysPlayFromStart;
    private bool playIndefinitely;
    private bool isProcessingStateChange;
    private string? lastCategoryName;
#if ANDROID
    private bool isUpdatingFromPermissionCheck;
    private bool isSyncingFromState;
    private bool isWaitingForPermissionResponse;
    private NotificationPermissionService? permissionService;
#elif IOS
    private bool isUpdatingFromPermissionCheck;
    private bool isSyncingFromState;
    private bool isWaitingForPermissionResponse;
    private IOSNotificationPermissionService? permissionService;
#else
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    private bool isUpdatingFromPermissionCheck;
    private bool isSyncingFromState;
    private bool isWaitingForPermissionResponse;
    private object? permissionService;
#pragma warning restore CS0649
#endif
    private readonly ContainerReadySignaler containerReadySignaler;
    private readonly NumberOfTracksListPopulator listPopulator;
    private readonly NumberOfTrackStateChangeHandler stateChangeHandler;

    private ObservableCollection<NumberOfTracksListViewItemModel> numberOfTracksList = new();
    private NumberOfTracksListViewItemModel? currentNumberOfTracks;
    private bool isBusy = true;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public NumberOfTrackContainerViewModel(
        ILogger logger,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        IState<ApplicationState> state,
        IDispatcher dispatcher)
    {
        this.logger = logger;
        this.navigationService = navigationService;
        this.serviceProvider = serviceProvider;
        this.state = state;
        this.dispatcher = dispatcher;
        containerReadySignaler = new ContainerReadySignaler(state, dispatcher, "NumberOfTrack", s => s.ContainerReadiness.NumberOfTrack);
        listPopulator = new NumberOfTracksListPopulator(logger, serviceProvider.GetService<Bible.Alarm.Shared.Services.Media.Interfaces.IBiblePublicationService>());
        stateChangeHandler = new NumberOfTrackStateChangeHandler(logger);

        state.StateChanged += OnStateChanged;
        InitializeCommands();
#if ANDROID || IOS
        InitializePermissionService();
        InitializeFromState();
#else
        InitializeFromState();
#endif
    }

#if ANDROID
    private void InitializePermissionService()
    {
        permissionService = NotificationPermissionService.Instance;
        
        // Subscribe to permission events
        permissionService.PermissionGranted += OnPermissionGranted;
        permissionService.PermissionDenied += OnPermissionDenied;
    }
#elif IOS
    private void InitializePermissionService()
    {
        permissionService = IOSNotificationPermissionService.Instance;
        
        // Subscribe to permission events
        permissionService.PermissionGranted += OnPermissionGranted;
        permissionService.PermissionDenied += OnPermissionDenied;
    }
#endif

#if ANDROID || IOS

    private void OnPermissionGranted(object? sender, EventArgs e)
    {
        logger.Information("NotificationPermissionService: Permission granted event received. isWaitingForPermissionResponse={IsWaiting}, currentNotificationEnabled={Current}", 
            isWaitingForPermissionResponse, notificationEnabled);
        
        // Only update if we're waiting for permission response
        if (isWaitingForPermissionResponse)
        {
            isUpdatingFromPermissionCheck = true;
            try
            {
                // Always set toggle to ON when permission is granted
                // Force update to ensure UI reflects the ON state
                var oldValue = notificationEnabled;
                notificationEnabled = true;
                OnPropertyChanged(nameof(NotificationEnabled));
                DispatchScheduleUpdate(s => s.NotificationEnabled = true);
                logger.Information("Set NotificationEnabled to true after permission granted. Old value: {OldValue}, New value: {NewValue}", 
                    oldValue, notificationEnabled);
            }
            finally
            {
                isUpdatingFromPermissionCheck = false;
                isWaitingForPermissionResponse = false;
            }
        }
        else
        {
            logger.Debug("Permission granted event received but not waiting for response - ignoring");
        }
    }

    private void OnPermissionDenied(object? sender, EventArgs e)
    {
        logger.Information("NotificationPermissionService: Permission denied event received");
        
        // Only show toast if we're waiting for permission response
        if (isWaitingForPermissionResponse)
        {
            isUpdatingFromPermissionCheck = true;
            try
            {
                notificationEnabled = false;
                OnPropertyChanged(nameof(NotificationEnabled));
                DispatchScheduleUpdate(s => s.NotificationEnabled = false);
                logger.Debug("Set NotificationEnabled to false after permission denied");

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await NotificationPermissionDeniedModalHelper.ShowAsync(
                        logger, navigationService, serviceProvider,
                        onPermissionGranted: () =>
                        {
                            isUpdatingFromPermissionCheck = true;
                            try
                            {
                                notificationEnabled = true;
                                OnPropertyChanged(nameof(NotificationEnabled));
                                DispatchScheduleUpdate(s => s.NotificationEnabled = true);
                                logger.Information("Set NotificationEnabled to true after permission granted from modal");
                            }
                            finally
                            {
                                isUpdatingFromPermissionCheck = false;
                            }
                        });
                });
            }
            finally
            {
                isUpdatingFromPermissionCheck = false;
                isWaitingForPermissionResponse = false;
            }
        }
    }
#endif

    private void InitializeCommands()
    {
        OpenModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.OpenNumberOfTracksModalAsync(this);
        });

        SelectNumberOfTracksCommand = new AsyncRelayCommand<NumberOfTracksListViewItemModel>(async x =>
        {
            if (CurrentNumberOfTracks != null)
            {
                CurrentNumberOfTracks.IsSelected = false;
            }

            CurrentNumberOfTracks = x;
            if (CurrentNumberOfTracks != null)
            {
                CurrentNumberOfTracks.IsSelected = true;
            }

            // Dispatch update to state
            if (CurrentNumberOfTracks != null)
            {
                DispatchScheduleUpdate(s => s.NumberOfTracksToPlay = CurrentNumberOfTracks.Value);
            }

            // Explicitly notify property changes to ensure UI binding updates
            OnPropertyChanged(nameof(CurrentNumberOfTracks));
            OnPropertyChanged(nameof(CurrentNumberOfTracksText));

            await navigationService.PopModalAsync();
        });

        ToggleAlwaysPlayFromStartCommand = new RelayCommand(() => AlwaysPlayFromStart = !AlwaysPlayFromStart);

        TogglePlayIndefinitelyCommand = new RelayCommand(() => PlayIndefinitely = !PlayIndefinitely);

        NotificationEnabledCommand = new RelayCommand(() => { NotificationEnabled = !NotificationEnabled; });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });
    }

    private void InitializeFromState()
    {
        try
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule != null)
            {
                scheduleId = currentSchedule.Id;
                notificationEnabled = currentSchedule.NotificationEnabled;
                alwaysPlayFromStart = currentSchedule.AlwaysPlayFromStart;
                playIndefinitely = currentSchedule.NumberOfTracksToPlay <= 0;
                lastCategoryName = currentSchedule.BiblePublicationCategoryName;

#if ANDROID || IOS
                notificationEnabled = NotificationPermissionSyncHelper.SyncValueWithPermission(
                    notificationEnabled,
                    () => permissionService != null && permissionService.IsGranted,
                    logger,
                    "InitializeFromState: NotificationEnabled is true in state but permission is not granted - setting local property to OFF",
                    "InitializeFromState: Exception checking notification permission",
                    () => { });
#endif

            _ = PopulateNumberOfTracksListViewAsync();

            OnPropertyChanged(nameof(NotificationEnabled));
            OnPropertyChanged(nameof(AlwaysPlayFromStart));
            OnPropertyChanged(nameof(PlayIndefinitely));
            OnPropertyChanged(nameof(IsNumberOfTracksSelectionVisible));
            OnPropertyChanged(nameof(TrackLabelText));
            OnPropertyChanged(nameof(ModalHeaderText));
            OnPropertyChanged(nameof(RestartLabelText));
            OnPropertyChanged(nameof(SelectedTracksText));

                // Signal that this container is ready (initialized from CurrentSchedule)
                containerReadySignaler.TrySignalReady();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "InitializeFromState: Exception initializing from state");
            // Don't rethrow - allow app to continue even if initialization fails
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // Prevent re-entrant calls to avoid cycles
        if (isProcessingStateChange)
        {
            return;
        }

        isProcessingStateChange = true;
        try
        {
            var stateValue = state.Value;
            var currentSchedule = stateValue.CurrentSchedule;

            // If ContainerReadiness was reset to NotReady but we've already signaled ready, reset our flag
            // This handles the case where ViewScheduleAction resets ContainerReadiness after containers signaled ready
            if (containerReadySignaler.HasSignaledReady && !stateValue.ContainerReadiness.NumberOfTrack && currentSchedule != null)
            {
                containerReadySignaler.Reset();
                // Re-initialize and signal ready again
                InitializeFromState();
                return;
            }

            // If we don't have a scheduleId yet (initial state), initialize when CurrentSchedule is set
            // But only if we haven't already signaled ready (prevents infinite loop for new schedules with Id=0)
            if (scheduleId == 0 && currentSchedule != null && !containerReadySignaler.HasSignaledReady)
            {
                InitializeFromState();
                return;
            }

            // Initialize if schedule ID changed to a different positive ID (existing schedule opened)
            if (currentSchedule != null && currentSchedule.Id != scheduleId && currentSchedule.Id > 0)
            {
                containerReadySignaler.Reset();
                InitializeFromState();
            }
            else if (currentSchedule != null)
            {
                isSyncingFromState = true;
                try
                {
                    var categoryBefore = lastCategoryName;
                    stateChangeHandler.ApplyPropertyChanges(
                        currentSchedule,
                        ref notificationEnabled,
                        ref alwaysPlayFromStart,
                        ref playIndefinitely,
                        ref lastCategoryName,
                        isWaitingForPermissionResponse,
#if ANDROID || IOS
                        () => permissionService != null && permissionService.IsGranted,
#else
                        () => true,
#endif
                        () => DispatchScheduleUpdate(s => s.NotificationEnabled = false),
                        forceSelection => PopulateNumberOfTracksListViewAsync(forceSelection),
                        () => DispatchScheduleUpdate(s => s.NumberOfTracksToPlay = 1));

                    OnPropertyChanged(nameof(NotificationEnabled));
                    OnPropertyChanged(nameof(AlwaysPlayFromStart));
                    OnPropertyChanged(nameof(PlayIndefinitely));
                    OnPropertyChanged(nameof(IsNumberOfTracksSelectionVisible));
                    if (!string.Equals(categoryBefore, lastCategoryName, StringComparison.OrdinalIgnoreCase))
                    {
                        OnPropertyChanged(nameof(TrackLabelText));
                        OnPropertyChanged(nameof(TracksLabelText));
                        OnPropertyChanged(nameof(SelectedTracksText));
                        OnPropertyChanged(nameof(ModalHeaderText));
                        OnPropertyChanged(nameof(RestartLabelText));
                    }
                }
                finally
                {
                    isSyncingFromState = false;
                }
            }
        }
        finally
        {
            isProcessingStateChange = false;
        }
    }

    public ICommand OpenModalCommand { get; private set; } = null!;
    public ICommand SelectNumberOfTracksCommand { get; private set; } = null!;
    public ICommand ToggleAlwaysPlayFromStartCommand { get; private set; } = null!;
    public ICommand TogglePlayIndefinitelyCommand { get; private set; } = null!;
    public ICommand NotificationEnabledCommand { get; private set; } = null!;
    public ICommand CloseModalCommand { get; private set; } = null!;

    public ObservableCollection<NumberOfTracksListViewItemModel> NumberOfTracksList
    {
        get => numberOfTracksList;
        set => SetProperty(ref numberOfTracksList, value);
    }

    public NumberOfTracksListViewItemModel? CurrentNumberOfTracks
    {
        get => currentNumberOfTracks;
        set
        {
            if (SetProperty(ref currentNumberOfTracks, value))
            {
                // Notify that the Text property (computed from CurrentNumberOfTracks) has changed
                OnPropertyChanged(nameof(CurrentNumberOfTracksText));
                OnPropertyChanged(nameof(SelectedTracksText));
                OnPropertyChanged(nameof(SelectedNumberText));
            }
        }
    }

    /// <summary>
    /// Computed property for binding to the number of tracks text in the UI.
    /// This ensures the UI updates when CurrentNumberOfTracks changes.
    /// </summary>
    public string CurrentNumberOfTracksText => CurrentNumberOfTracks?.Text ?? string.Empty;

    private string? CategoryName => state.Value.CurrentSchedule?.BiblePublicationCategoryName;

    public string TrackLabelText =>
        TracksUnitTextProvider.GetTrackLabelText(CategoryName);

    public string TracksLabelText =>
        TracksUnitTextProvider.GetTracksLabelText(CategoryName);

    public string SelectedTracksText =>
        TracksUnitTextProvider.GetSelectedTracksText(CategoryName, CurrentNumberOfTracks?.Value ?? 0);

    public string SelectedNumberText => (CurrentNumberOfTracks?.Value ?? 0).ToString();

    public string ModalHeaderText =>
        TracksUnitTextProvider.GetModalHeaderText(CategoryName);

    public string RestartLabelText =>
        TracksUnitTextProvider.GetRestartLabelText(CategoryName);

    public bool NotificationEnabled
    {
        get => notificationEnabled;
        set
        {
            // Prevent re-entrancy - if we're already processing, ignore
            if (isUpdatingFromPermissionCheck || isSyncingFromState)
            {
                logger.Debug("NotificationEnabled setter called during update/sync - ignoring. isUpdatingFromPermissionCheck={IsUpdating}, isSyncingFromState={IsSyncing}, value={Value}", 
                    isUpdatingFromPermissionCheck, isSyncingFromState, value);
                return;
            }
            
            var isUserAction = !isSyncingFromState;

#if ANDROID || IOS
            if (isUserAction && value &&
                NotificationEnabledToggleHandler.TryHandleToggleOnWhenNotGranted(
                    value,
                    () => permissionService != null && permissionService.IsGranted,
                    () => permissionService?.RequestPermissionIfNeeded() ?? false,
                    () =>
                    {
                        notificationEnabled = false;
                        OnPropertyChanged(nameof(NotificationEnabled));
                        DispatchScheduleUpdate(s => s.NotificationEnabled = false);
                    },
                    () =>
                    {
                        notificationEnabled = true;
                        OnPropertyChanged(nameof(NotificationEnabled));
                        DispatchScheduleUpdate(s => s.NotificationEnabled = true);
                    },
                    x => isWaitingForPermissionResponse = x,
                    logger,
                    "NotificationEnabled setter"))
            {
                return;
            }

            if (SetProperty(ref notificationEnabled, value))
            {
                DispatchScheduleUpdate(s => s.NotificationEnabled = value);
            }

            if (isUserAction && !value)
            {
                isWaitingForPermissionResponse = false;
            }
#else
            // Non-Android/iOS platforms - update immediately
            if (SetProperty(ref notificationEnabled, value))
            {
                DispatchScheduleUpdate(s => s.NotificationEnabled = value);
            }
#endif
        }
    }

#if ANDROID || IOS
    public void StopPermissionCheckTaskIfRunning()
    {
        // Reset waiting flag if task was running
        isWaitingForPermissionResponse = false;
    }
#endif

    public bool AlwaysPlayFromStart
    {
        get => alwaysPlayFromStart;
        set
        {
            if (SetProperty(ref alwaysPlayFromStart, value))
            {
                DispatchScheduleUpdate(s => s.AlwaysPlayFromStart = value);
            }
        }
    }

    /// <summary>
    /// When enabled, the schedule plays indefinitely (NumberOfTracksToPlay is stored as 0).
    /// When disabled, the user selects a finite number of chapters/episodes to play.
    /// </summary>
    public bool PlayIndefinitely
    {
        get => playIndefinitely;
        set
        {
            if (!SetProperty(ref playIndefinitely, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsNumberOfTracksSelectionVisible));

            if (playIndefinitely)
            {
                // Store 0 to indicate indefinite playback
                DispatchScheduleUpdate(s => s.NumberOfTracksToPlay = 0);
                return;
            }

            // Switching back to finite mode:
            // Ensure a valid selection exists, otherwise apply a sensible default.
            const int defaultTracks = 1;
            var selected = CurrentNumberOfTracks?.Value ?? defaultTracks;
            if (selected <= 0)
            {
                selected = defaultTracks;
            }

            // Ensure UI list has a selection even if schedule previously stored 0.
            _ = PopulateNumberOfTracksListViewAsync(selected);
            DispatchScheduleUpdate(s => s.NumberOfTracksToPlay = selected);
        }
    }

    /// <summary>
    /// True when the finite number-of-tracks row should be shown.
    /// </summary>
    public bool IsNumberOfTracksSelectionVisible => !PlayIndefinitely;

    /// <summary>
    /// Populates the number of tracks list view.
    /// This method is async because it may need to fetch publication data for dramas.
    /// </summary>
    public async Task PopulateNumberOfTracksListViewAsync(int? forceSelection = null)
    {
        var preservedSelection = forceSelection ?? CurrentNumberOfTracks?.Value;
        var currentSchedule = state.Value.CurrentSchedule;

        var result = await listPopulator.PopulateAsync(
            currentSchedule,
            preservedSelection,
            CurrentNumberOfTracks?.Value);

        NumberOfTracksList = result.List;
        if (result.SelectedItem != null)
        {
            CurrentNumberOfTracks = result.SelectedItem;
        }

        OnPropertyChanged(nameof(NumberOfTracksList));
    }

    /// <summary>
    /// Private wrapper for backward compatibility with fire-and-forget calls.
    /// </summary>
    private async void PopulateNumberOfTracksListView(int? forceSelection = null)
    {
        await PopulateNumberOfTracksListViewAsync(forceSelection);
    }

    private void DispatchScheduleUpdate(Action<ScheduleStateItem> updateAction)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return;
        }

        // Clone the current schedule and apply the update
        var updatedSchedule = ScheduleStateHelper.CloneScheduleStateItem(currentSchedule);
        updateAction(updatedSchedule);
        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
#if ANDROID
        if (permissionService != null)
        {
            permissionService.PermissionGranted -= OnPermissionGranted;
            permissionService.PermissionDenied -= OnPermissionDenied;
        }
#endif
    }
}

