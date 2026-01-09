#nullable enable
using AutoMapper;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Handles navigation logic for HomeViewModel.
/// Separated from HomeViewModel for better modularity.
/// </summary>
public class HomeNavigationHelper
{
    private readonly ILogger logger;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IState<ApplicationState> state;
    private readonly IState<PlaybackState> playbackState;
    private readonly IMapper mapper;
    private readonly IAlarmScheduleService? alarmScheduleService;
    private readonly IScheduleDisplayNameService? scheduleDisplayNameService;
    private readonly Dictionary<int, DateTime> recentPlayClicks = new();
    private const int PlayClickCooldownMs = 500;

    public Action<int> TrackPlayClick => (scheduleId) => recentPlayClicks[scheduleId] = DateTime.UtcNow;

    public HomeNavigationHelper(
        ILogger logger,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IState<ApplicationState> state,
        IState<PlaybackState> playbackState,
        IMapper mapper,
        IAlarmScheduleService? alarmScheduleService = null,
        IScheduleDisplayNameService? scheduleDisplayNameService = null)
    {
        this.logger = logger;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.state = state;
        this.playbackState = playbackState;
        this.mapper = mapper;
        this.alarmScheduleService = alarmScheduleService;
        this.scheduleDisplayNameService = scheduleDisplayNameService;
    }

    public bool ShouldSkipNavigation(int scheduleId, Dictionary<int, ScheduleListItemViewModel> scheduleViewModels)
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

    public async Task ShowOverlayAndNavigateAsync(ScheduleListItemViewModel scheduleListItem, Dictionary<int, ScheduleListItemViewModel> scheduleViewModels)
    {
        if (scheduleListItem.Schedule == null)
        {
            return;
        }

        scheduleListItem.Schedule.IsEnabled = scheduleListItem.IsEnabled;

        ScheduleStateItem? sourceScheduleStateItem;
        
        // For existing schedules, load fresh from DB to ensure latest data (especially music track number)
        if (scheduleListItem.Schedule.Id > 0)
        {
            sourceScheduleStateItem = await LoadScheduleFromDatabaseAsync(scheduleListItem.Schedule.Id);
            if (sourceScheduleStateItem == null)
            {
                // Fallback to state if DB load fails
                logger.Warning("Failed to load schedule {ScheduleId} from DB, falling back to state", scheduleListItem.Schedule.Id);
                sourceScheduleStateItem = GetSourceScheduleStateItem(scheduleListItem.Schedule.Id, scheduleViewModels);
            }
            else
            {
                // Preserve IsEnabled from the list item
                sourceScheduleStateItem.IsEnabled = scheduleListItem.IsEnabled;
            }
        }
        else
        {
            // For new schedules, use state
            sourceScheduleStateItem = GetSourceScheduleStateItem(scheduleListItem.Schedule.Id, scheduleViewModels);
        }

        // Reset container readiness first to ensure containers signal ready on new page
        // ViewScheduleAction will also reset it, but doing it explicitly ensures clean state
        dispatcher.Dispatch(new ResetContainerReadinessAction());
        
        // Dispatch action to set CurrentSchedule BEFORE navigation
        // This prevents ScheduleStateManager from creating a sample schedule
        // ViewScheduleAction will reset container readiness to NotReady
        dispatcher.Dispatch(new ViewScheduleAction(sourceScheduleStateItem));

        // Navigate AFTER dispatching action so CurrentSchedule is already set when page initializes
        // A fresh Schedule page and ViewModel will be created (both registered as Transient)
        // Fresh container ViewModels will be created and assigned
        await navigationService.NavigateToScheduleAsync();
    }

    private ScheduleStateItem GetSourceScheduleStateItem(int scheduleId, Dictionary<int, ScheduleListItemViewModel> scheduleViewModels)
    {
        var scheduleStateItem = state.Value.Schedules?.FirstOrDefault(s => s.Id == scheduleId);
        if (scheduleStateItem == null)
        {
            return mapper.Map<ScheduleStateItem>(scheduleViewModels[scheduleId].Schedule);
        }
        return scheduleStateItem;
    }

    /// <summary>
    /// Loads a schedule from the database and maps it to a ScheduleStateItem with display names populated.
    /// This ensures we always have the latest data when viewing an existing schedule.
    /// </summary>
    private async Task<ScheduleStateItem?> LoadScheduleFromDatabaseAsync(int scheduleId)
    {
        if (alarmScheduleService == null || scheduleDisplayNameService == null)
        {
            logger.Debug("Cannot load schedule from DB - required services not available");
            return null;
        }

        try
        {
            // Load schedule from database with all includes
            var schedule = await alarmScheduleService.GetScheduleByIdAsync(
                scheduleId,
                includeMusic: true,
                includeBibleReading: true,
                CancellationToken.None);

            if (schedule == null)
            {
                logger.Warning("Schedule {ScheduleId} not found in database", scheduleId);
                return null;
            }

            // Map to ScheduleStateItem
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(schedule);

            // Populate display names
            await scheduleDisplayNameService.PopulateDisplayNamesAsync(scheduleStateItem, schedule);

            logger.Debug("Loaded schedule {ScheduleId} from database with latest data", scheduleId);
            return scheduleStateItem;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error loading schedule {ScheduleId} from database", scheduleId);
            return null;
        }
    }
}

