#nullable enable

using System.Linq;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Maui.Essentials;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels;

public sealed class HomeViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly IServiceScopeFactory scopeFactory;

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

    public HomeViewModel(
        ILogger logger,
        IServiceScopeFactory scopeFactory,
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
        navigationHelper = new HomeNavigationHelper(logger, dispatcher, navigationService, state, playbackState, mapper);
        scheduleViewModelManager = new ScheduleViewModelManager(logger, scopeFactory, navigationHelper.TrackPlayClick);
        progressAnimator = new ProgressBarAnimator();
        progressBarManager = new ProgressBarManager(progressAnimator);
        propertyManager = new PropertyManager();

        // Setup progress bar manager events
        progressBarManager.ProgressBarOpacityChanged += (opacity) =>
        {
            OnPropertyChanged(nameof(ProgressBarOpacity));
        };
        progressBarManager.ProgressBarHiddenChanged += () =>
        {
            OnPropertyChanged(nameof(IsProgressBarHidden));
        };
        progressAnimator.ProgressChanged += () =>
        {
            OnPropertyChanged(nameof(AnimatedProgressStart));
            OnPropertyChanged(nameof(AnimatedProgressEnd));
            OnPropertyChanged(nameof(AnimatedProgress));
            OnPropertyChanged(nameof(AnimatedProgressRangeWidth));
        };

        // Setup property manager events
        propertyManager.SchedulesChanged += () =>
        {
            OnPropertyChanged(nameof(Schedules));
            progressBarManager.UpdateVisibility(propertyManager.IsBusy, propertyManager.Schedules?.Count);
        };
        propertyManager.IsBusyChanged += (isBusy) =>
        {
            OnPropertyChanged(nameof(IsBusy));
            progressBarManager.UpdateVisibility(isBusy, propertyManager.Schedules?.Count);
        };

        // Initialize command handler
        commandHandler = new CommandHandler(
            dispatcher,
            navigationService,
            (x) => navigationHelper.ShouldSkipNavigation(x.Schedule.Id, scheduleViewModelManager.ScheduleViewModels),
            async (x) => await navigationHelper.ShowOverlayAndNavigateAsync(x, scheduleViewModelManager.ScheduleViewModels));

        AddScheduleCommand = commandHandler.CreateAddScheduleCommand();
        ViewScheduleCommand = commandHandler.CreateViewScheduleCommand();

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

    public ScheduleViewModel? SelectedSchedule
    {
        get => propertyManager.SelectedSchedule;
        set => propertyManager.SelectedSchedule = value;
    }

    public bool IsHomePageOverlayVisible => propertyManager.IsHomePageOverlayVisible;

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
    /// Hides the Schedule page overlay. Called when navigating back to Home page.
    /// </summary>
    public void HideSchedulePageOverlay() => dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });

    /// <summary>
    /// Resets all schedule-related state when navigating back to home.
    /// This ensures only one schedule is in state at any time.
    /// </summary>
    public void ResetScheduleState() => dispatcher.Dispatch(new ResetScheduleStateAction());


    public ICommand AddScheduleCommand { get; set; }
    public ICommand ViewScheduleCommand { get; set; }


    private void OnStateChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await stateChangeHandler.HandleStateChangedAsync(stateValue);
        });
    }


    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
        progressBarManager.Dispose();
        scheduleViewModelManager.DisposeAll();
    }
}
