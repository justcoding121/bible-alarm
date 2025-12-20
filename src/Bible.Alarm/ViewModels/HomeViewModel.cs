#nullable enable

using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels;

public class HomeViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly IServiceScopeFactory scopeFactory;

    private readonly IDatabaseSeedService databaseSeedService;
    private readonly IScheduleMigrationService scheduleMigrationService;
    private readonly IAlarmScheduleService alarmScheduleService;

    private readonly Dictionary<int, ScheduleListItem> scheduleViewModels = [];

    private readonly Func<AlarmSchedule, ScheduleListItem> scheduleListItemFactory;
    private readonly IMapper mapper;

    private readonly IDispatcher dispatcher;
    private readonly IState<ApplicationState> state;

    public HomeViewModel(
        ILogger logger,
        IServiceScopeFactory scopeFactory,
        Func<AlarmSchedule, ScheduleListItem> scheduleListItemFactory,
        IState<ApplicationState> state,
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
        this.dispatcher = dispatcher;
        this.databaseSeedService = databaseSeedService;
        this.scheduleMigrationService = scheduleMigrationService;
        this.alarmScheduleService = alarmScheduleService;
        this.mapper = mapper;

        AddScheduleCommand = new AsyncRelayCommand(async () =>
        {
            // Show overlay immediately via state
            dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = true });

            // Wait for state to update and UI to reflect the change
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Force property change notification
                OnPropertyChanged(nameof(IsHomePageOverlayVisible));
                // Wait longer to ensure UI has fully rendered the overlay before navigation
                await Task.Delay(150);
            });

            // Reset schedule state before creating a new schedule
            // This ensures only one schedule is in state at any time
            dispatcher.Dispatch(new ResetScheduleStateAction());
            await navigationService.NavigateToScheduleAsync();
            dispatcher.Dispatch(new ViewScheduleAction(null));
        });

        ViewScheduleCommand = new AsyncRelayCommand<ScheduleListItem>(async x =>
        {
            if (x == null || x.Schedule == null)
            {
                return;
            }

            // Show overlay immediately via state
            dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = true });

            // Wait for state to update and UI to reflect the change
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Force property change notification
                OnPropertyChanged(nameof(IsHomePageOverlayVisible));
                // Wait longer to ensure UI has fully rendered the overlay before navigation
                await Task.Delay(150);
            });

            x.Schedule.IsEnabled = x.IsEnabled;
            // Start navigation immediately (don't await yet)
            var navigationTask = navigationService.NavigateToScheduleAsync();
            // Map AlarmSchedule to ScheduleStateItem before dispatching
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(x.Schedule);
            // Dispatch action immediately so data loading can start
            dispatcher.Dispatch(new ViewScheduleAction(scheduleStateItem));
            // Wait for navigation to complete
            await navigationTask;
            // Don't hide overlay here - it will be hidden by ScheduleViewModel after navigation completes
        });

        state.StateChanged += OnStateChanged;

        // Immediately process current state when ViewModel is created
        // This ensures the ListView is populated with fresh data when a new Home page is created
        OnStateChanged(this, EventArgs.Empty);

        // Don't set IsBusy = false here - it will be set after initialization completes
        // IsBusy starts as true and will be set to false in OnStateChanged after schedules are populated
    }


    private ObservableHashSet<ScheduleListItem> schedules = [];

    public ObservableHashSet<ScheduleListItem> Schedules
    {
        get => schedules;
        set => SetProperty(ref schedules, value);
    }

    private void UpdateScheduleViewModels(ObservableHashSet<ScheduleStateItem> scheduleItems)
    {
        var currentViewModelIds = new HashSet<int>();
        var schedulesToAdd = new List<ScheduleListItem>();
        var schedulesToRemove = new List<int>();

        // Initialize Schedules if null
        Schedules ??= [];

        // Create a snapshot of the schedule items collection to avoid "Collection was modified" exception
        // This can happen when state changes occur rapidly (e.g., track transitions)
        var scheduleItemsSnapshot = scheduleItems.ToList();

        // Process each schedule item from the snapshot
        foreach (var scheduleItem in scheduleItemsSnapshot)
        {
            // Map ScheduleStateItem back to AlarmSchedule for ScheduleListItem
            // ScheduleListItem needs AlarmSchedule entity (not DTO)
            var schedule = mapper.Map<AlarmSchedule>(scheduleItem);
            currentViewModelIds.Add(schedule.Id);

            if (scheduleViewModels.TryGetValue(schedule.Id, out var existingViewModel))
            {
                // Existing view model - just update it in place
                // Initialize() will trigger property change notifications for this specific item
                existingViewModel.Initialize(schedule);
                // Ensure callbacks are set (in case it was created before we added this logic)
                if (existingViewModel.OnPlayStarted == null)
                {
                    existingViewModel.OnPlayStarted = () =>
                    {
                        dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = true });
                    };
                }
                if (existingViewModel.OnPlaybackStarted == null)
                {
                    existingViewModel.OnPlaybackStarted = () =>
                    {
                        dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = false });
                    };
                }
            }
            else
            {
                // New schedule - create new view model
                var viewModel = scheduleListItemFactory(schedule);
                // Set callbacks to show/hide overlay when play is pressed/started
                viewModel.OnPlayStarted = () =>
                {
                    dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = true });
                };
                viewModel.OnPlaybackStarted = () =>
                {
                    dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = false });
                };
                scheduleViewModels[schedule.Id] = viewModel;
                schedulesToAdd.Add(viewModel);
            }
        }

        // Identify view models to remove
        var toRemove = scheduleViewModels.Keys.Where(id => !currentViewModelIds.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            if (!scheduleViewModels.TryGetValue(id, out var viewModel))
            {
                continue;
            }

            schedulesToRemove.Add(id);
            viewModel.Dispose();
            scheduleViewModels.Remove(id);
        }

        // Only replace the collection if schedules were added or removed
        // For updates to existing schedules, Initialize() already updated the view model properties
        // and triggered property change notifications on that specific item, so no collection replacement needed
        if (schedulesToAdd.Count > 0 || schedulesToRemove.Count > 0)
        {
            // Create new collection only when items are added/removed
            var newSchedules = new ObservableHashSet<ScheduleListItem>();

            // Create a snapshot of current Schedules to avoid "Collection was modified" exception
            var currentSchedulesSnapshot = Schedules.ToList();

            // Add all existing items that aren't being removed
            foreach (var item in currentSchedulesSnapshot)
            {
                if (item.ScheduleId > 0 && !schedulesToRemove.Contains(item.ScheduleId))
                {
                    newSchedules.Add(item);
                }
            }

            // Add new items
            foreach (var item in schedulesToAdd)
            {
                newSchedules.Add(item);
            }

            Schedules = newSchedules;
        }
        // If no adds/removes, the collection stays the same and only individual item properties are updated
        // This prevents the entire list from reloading when a single item changes
    }

    private bool isBusy = true;

    public bool IsBusy
    {
        get => isBusy;
        set
        {
            SetProperty(ref isBusy, value);
            Loaded = !isBusy;
        }
    }

    /// <summary>
    /// Gets the overlay visibility from application state.
    /// This property is bound to the Home page overlay.
    /// </summary>
    public bool IsHomePageOverlayVisible => state.Value.IsHomePageOverlayVisible;

    /// <summary>
    /// Hides the Schedule page overlay. Called when navigating back to Home page.
    /// </summary>
    public void HideSchedulePageOverlay() => dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });

    /// <summary>
    /// Resets all schedule-related state when navigating back to home.
    /// This ensures only one schedule is in state at any time.
    /// </summary>
    public void ResetScheduleState() => dispatcher.Dispatch(new ResetScheduleStateAction());


    private bool loaded;

    public bool Loaded
    {
        get => loaded;
        set => SetProperty(ref loaded, value);
    }

    public ICommand AddScheduleCommand { get; set; }
    public ICommand ViewScheduleCommand { get; set; }

    private ScheduleViewModel? selectedSchedule;

    public ScheduleViewModel? SelectedSchedule
    {
        get => selectedSchedule;
        set => SetProperty(ref selectedSchedule, value);
    }


    private void OnStateChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;

        _ = MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Always notify about overlay visibility changes
            OnPropertyChanged(nameof(IsHomePageOverlayVisible));

            if (stateValue.Schedules != null)
            {
                UpdateScheduleViewModels(stateValue.Schedules);
                IsBusy = false;
            }
        });
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;

        if (Schedules != null)
        {
            foreach (var item in Schedules)
            {
                item.Dispose();
            }
        }

    }
}
