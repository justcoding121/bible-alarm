#nullable enable

using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.General;
using Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer.ListPopulation;
using Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer.StateInitialization;
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

public sealed class NumberOfTrackContainerViewModel : ObservableObject, IListViewModel, IDisposable
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
    private readonly IServiceProvider serviceProvider;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;

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
#endif
    private readonly ContainerReadySignaler containerReadySignaler;
    private readonly NumberOfTracksListPopulator listPopulator;
    private readonly NumberOfTrackStateChangeHandler stateChangeHandler;
    private readonly NumberOfTrackStateChangeOrchestrator stateChangeOrchestrator;

    private ObservableCollection<NumberOfTracksListViewItemModel> numberOfTracksList = new();
    private NumberOfTracksListViewItemModel? currentNumberOfTracks;
    private bool isBusy = true;
    private bool isCancelBusy;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public object? SelectedItem => CurrentNumberOfTracks;

    public NumberOfTrackContainerViewModel(
        ILogger logger,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        IMapper mapper)
    {
        this.logger = logger;
        this.navigationService = navigationService;
        this.serviceProvider = serviceProvider;
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;
        containerReadySignaler = new ContainerReadySignaler(state, dispatcher, "NumberOfTrack", s => s.ContainerReadiness.NumberOfTrack);
        listPopulator = new NumberOfTracksListPopulator(logger, serviceProvider.GetService<Bible.Alarm.Shared.Services.Media.Interfaces.IBiblePublicationService>());
        stateChangeHandler = new NumberOfTrackStateChangeHandler(logger);
        stateChangeOrchestrator = new NumberOfTrackStateChangeOrchestrator(containerReadySignaler);

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
        permissionService.PermissionGranted += OnPermissionGranted;
        permissionService.PermissionDenied += OnPermissionDenied;
    }
#elif IOS
    private void InitializePermissionService()
    {
        permissionService = IOSNotificationPermissionService.Instance;
        permissionService.PermissionGranted += OnPermissionGranted;
        permissionService.PermissionDenied += OnPermissionDenied;
    }
#endif

#if ANDROID || IOS

    private void OnPermissionGranted(object? sender, EventArgs e) =>
        NumberOfTrackPermissionHandlers.HandlePermissionGranted(
            isWaitingForPermissionResponse,
            logger,
            () =>
            {
                notificationEnabled = true;
                OnPropertyChanged(nameof(NotificationEnabled));
                DispatchScheduleUpdate(s => s.NotificationEnabled = true);
                logger.Information("Set NotificationEnabled to true after permission granted");
            },
            x => isUpdatingFromPermissionCheck = x,
            x => isWaitingForPermissionResponse = x);

    private void OnPermissionDenied(object? sender, EventArgs e) =>
        NumberOfTrackPermissionHandlers.HandlePermissionDenied(
            isWaitingForPermissionResponse,
            logger,
            navigationService,
            serviceProvider,
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
                logger.Information("Set NotificationEnabled to true after permission granted from modal");
            },
            x => isUpdatingFromPermissionCheck = x,
            x => isWaitingForPermissionResponse = x);
#endif

    private void InitializeCommands()
    {
        OpenModalCommand = new AsyncRelayCommand(async () => await navigationService.OpenNumberOfTracksModalAsync(this));
        SelectNumberOfTracksCommand = new AsyncRelayCommand<NumberOfTracksListViewItemModel>(OnSelectNumberOfTracksAsync);
        ToggleAlwaysPlayFromStartCommand = new RelayCommand(() => AlwaysPlayFromStart = !AlwaysPlayFromStart);
        TogglePlayIndefinitelyCommand = new RelayCommand(() => PlayIndefinitely = !PlayIndefinitely);
        NotificationEnabledCommand = new RelayCommand(() => { NotificationEnabled = !NotificationEnabled; });
        CloseModalCommand = new AsyncRelayCommand(async () => await navigationService.PopModalAsync());
        OverlayCancelCommand = new AsyncRelayCommand(OverlayCancelAsync);
    }

    private async Task OverlayCancelAsync()
    {
        IsCancelBusy = true;
        await Task.Delay(50);
        try
        {
            await navigationService.PopModalAsync();
        }
        finally
        {
            IsCancelBusy = false;
        }
    }

    private async Task OnSelectNumberOfTracksAsync(NumberOfTracksListViewItemModel? x)
    {
        if (CurrentNumberOfTracks != null) CurrentNumberOfTracks.IsSelected = false;
        CurrentNumberOfTracks = x;
        if (CurrentNumberOfTracks != null) CurrentNumberOfTracks.IsSelected = true;
        if (CurrentNumberOfTracks != null) DispatchScheduleUpdate(s => s.NumberOfTracksToPlay = CurrentNumberOfTracks!.Value);
        OnPropertyChanged(nameof(CurrentNumberOfTracks));
        OnPropertyChanged(nameof(CurrentNumberOfTracksText));
        await navigationService.PopModalAsync();
    }

    private void InitializeFromState()
    {
        try
        {
            var result = NumberOfTrackStateInitializer.TryInitialize(
                state.Value.CurrentSchedule,
#if ANDROID || IOS
                () => permissionService != null && permissionService.IsGranted,
#else
                () => true,
#endif
                logger);

            if (result == null)
            {
                return;
            }

            scheduleId = result.ScheduleId;
            notificationEnabled = result.NotificationEnabled;
            alwaysPlayFromStart = result.AlwaysPlayFromStart;
            playIndefinitely = result.PlayIndefinitely;
            lastCategoryName = result.LastCategoryName;

            _ = PopulateNumberOfTracksListViewAsync();
            OnPropertyChanged(nameof(NotificationEnabled));
            OnPropertyChanged(nameof(AlwaysPlayFromStart));
            OnPropertyChanged(nameof(PlayIndefinitely));
            OnPropertyChanged(nameof(IsNumberOfTracksSelectionVisible));
            OnPropertyChanged(nameof(TrackLabelText));
            OnPropertyChanged(nameof(ModalHeaderText));
            OnPropertyChanged(nameof(RestartLabelText));
            OnPropertyChanged(nameof(SelectedTracksText));
            containerReadySignaler.TrySignalReady();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "InitializeFromState: Exception initializing from state");
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (isProcessingStateChange)
        {
            return;
        }

        isProcessingStateChange = true;
        try
        {
            var stateValue = state.Value;
            var currentSchedule = stateValue.CurrentSchedule;
            var (shouldReset, shouldReinit) = stateChangeOrchestrator.GetReinitDecision(stateValue, scheduleId);

            if (shouldReinit)
            {
                if (shouldReset)
                {
                    containerReadySignaler.Reset();
                }
                InitializeFromState();
                return;
            }

            if (currentSchedule != null)
            {
#if ANDROID || IOS
                isSyncingFromState = true;
                try
#endif
                {
                    var categoryBefore = lastCategoryName;
                    stateChangeHandler.ApplyPropertyChanges(
                        currentSchedule,
                        ref notificationEnabled,
                        ref alwaysPlayFromStart,
                        ref playIndefinitely,
                        ref lastCategoryName,
#if ANDROID || IOS
                        isWaitingForPermissionResponse,
#else
                        false,
#endif
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
#if ANDROID || IOS
                finally
                {
                    isSyncingFromState = false;
                }
#endif
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
    public ICommand OverlayCancelCommand { get; private set; } = null!;

    public bool IsCancelBusy
    {
        get => isCancelBusy;
        set => SetProperty(ref isCancelBusy, value);
    }

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
#if ANDROID || IOS
            if (isUpdatingFromPermissionCheck || isSyncingFromState)
            {
                logger.Debug("NotificationEnabled setter called during update/sync - ignoring. isUpdatingFromPermissionCheck={IsUpdating}, isSyncingFromState={IsSyncing}, value={Value}", 
                    isUpdatingFromPermissionCheck, isSyncingFromState, value);
                return;
            }

            var isUserAction = !isSyncingFromState;
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
            if (SetProperty(ref notificationEnabled, value))
            {
                DispatchScheduleUpdate(s => s.NotificationEnabled = value);
            }
#endif
        }
    }

#if ANDROID || IOS
    public void StopPermissionCheckTaskIfRunning() => isWaitingForPermissionResponse = false;
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

    public bool IsNumberOfTracksSelectionVisible => !PlayIndefinitely;

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

    private void DispatchScheduleUpdate(Action<ScheduleStateItem> updateAction)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return;
        }

        var updatedSchedule = mapper.Map<ScheduleStateItem>(currentSchedule);
        updateAction(updatedSchedule);
        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
#if ANDROID || IOS
        if (permissionService != null)
        {
            permissionService.PermissionGranted -= OnPermissionGranted;
            permissionService.PermissionDenied -= OnPermissionDenied;
        }
#endif
    }
}

