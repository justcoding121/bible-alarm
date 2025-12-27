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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
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

    private readonly Dictionary<int, ScheduleListItem> scheduleViewModels = [];

    private readonly Func<int, ScheduleListItem> scheduleListItemFactory;
    private readonly IMapper mapper;

    private readonly IDispatcher dispatcher;
    private readonly IState<ApplicationState> state;
    private readonly IState<PlaybackState> playbackState;
    private readonly INavigationService navigationService;

    // Track recent play button clicks to prevent navigation race condition
    private readonly Dictionary<int, DateTime> recentPlayClicks = new();
    private const int PlayClickCooldownMs = 500; // 500ms cooldown after play click

    public HomeViewModel(
        ILogger logger,
        IServiceScopeFactory scopeFactory,
        Func<int, ScheduleListItem> scheduleListItemFactory,
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

            if (ShouldSkipNavigation(x.Schedule.Id))
            {
                return;
            }

            await ShowOverlayAndNavigateAsync(x);
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
        // Filter out unsaved schedules (ID <= 0) - these should not appear in the home list
        foreach (var scheduleItem in scheduleItemsSnapshot)
        {
            var scheduleId = scheduleItem.Id;

            // Skip unsaved schedules (ID <= 0) - they should not appear in the home list
            if (scheduleId <= 0)
            {
                continue;
            }

            currentViewModelIds.Add(scheduleId);

            if (scheduleViewModels.TryGetValue(scheduleId, out var existingViewModel))
            {
                // Existing view model - update it from state
                // SetScheduleId() will trigger property change notifications for this specific item
                existingViewModel.SetScheduleId(scheduleId);
                // Ensure callbacks are set (in case it was created before we added this logic)
                if (existingViewModel.OnPlayStarted == null)
                {
                    existingViewModel.OnPlayStarted = () =>
                    {
                        // Track play button click to prevent navigation
                        recentPlayClicks[scheduleId] = DateTime.UtcNow;
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
                // New schedule - create new view model using schedule ID
                // ScheduleListItem will initialize from state using the ID
                var viewModel = scheduleListItemFactory(scheduleId);
                // Set callbacks to show/hide overlay when play is pressed/started
                viewModel.OnPlayStarted = () =>
                {
                    // Track play button click to prevent navigation
                    recentPlayClicks[scheduleId] = DateTime.UtcNow;
                    dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = true });
                };
                viewModel.OnPlaybackStarted = () =>
                {
                    dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = false });
                };
                scheduleViewModels[scheduleId] = viewModel;
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
        // For updates to existing schedules, SetScheduleId() already updated the view model properties
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
            // Notify overlay visibility change when IsBusy changes
            OnPropertyChanged(nameof(IsHomePageOverlayVisible));
        }
    }

    /// <summary>
    /// Gets the overlay visibility from application state.
    /// Shows overlay when schedules are loading (IsBusy) or when explicitly set via state.
    /// This property is bound to the Home page overlay.
    /// </summary>
    public bool IsHomePageOverlayVisible => state.Value.IsHomePageOverlayVisible || IsBusy;

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
                // Notify overlay visibility change after IsBusy is set to false
                OnPropertyChanged(nameof(IsHomePageOverlayVisible));
            }
        });
    }

    private bool ShouldSkipNavigation(int scheduleId)
    {
        var currentPlaybackState = playbackState.Value;
        if (currentPlaybackState.IsPreparingOrPlaying &&
            currentPlaybackState.CurrentScheduleId == scheduleId)
        {
            logger.Debug("ViewScheduleCommand: Skipping navigation - playback is active for schedule {ScheduleId}", scheduleId);
            return true;
        }

        if (recentPlayClicks.TryGetValue(scheduleId, out var playClickTime))
        {
            var timeSincePlayClick = (DateTime.UtcNow - playClickTime).TotalMilliseconds;
            if (timeSincePlayClick < PlayClickCooldownMs)
            {
                logger.Debug("ViewScheduleCommand: Skipping navigation - play button was clicked {TimeSinceClick}ms ago for schedule {ScheduleId}",
                    timeSincePlayClick, scheduleId);
                return true;
            }
            recentPlayClicks.Remove(scheduleId);
        }

        return false;
    }

    private async Task ShowOverlayAndNavigateAsync(ScheduleListItem scheduleListItem)
    {
        dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = true });

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            OnPropertyChanged(nameof(IsHomePageOverlayVisible));
            await Task.Delay(150);
        });

        if (scheduleListItem.Schedule == null)
        {
            return;
        }

        scheduleListItem.Schedule.IsEnabled = scheduleListItem.IsEnabled;
        var navigationTask = navigationService.NavigateToScheduleAsync();
        var scheduleStateItem = GetScheduleStateItem(scheduleListItem.Schedule.Id);
        dispatcher.Dispatch(new ViewScheduleAction(scheduleStateItem));
        await navigationTask;
    }

    private ScheduleStateItem GetScheduleStateItem(int scheduleId)
    {
        var scheduleStateItem = state.Value.Schedules?.FirstOrDefault(s => s.Id == scheduleId);
        if (scheduleStateItem == null)
        {
            return mapper.Map<ScheduleStateItem>(scheduleViewModels[scheduleId].Schedule);
        }
        return scheduleStateItem.DeepClone();
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
