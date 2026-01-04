#nullable enable
using AutoMapper;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.DataStructures;
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
    private readonly Dictionary<int, DateTime> recentPlayClicks = new();
    private const int PlayClickCooldownMs = 500;

    public Action<int> TrackPlayClick => (scheduleId) => recentPlayClicks[scheduleId] = DateTime.UtcNow;

    public HomeNavigationHelper(
        ILogger logger,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IState<ApplicationState> state,
        IState<PlaybackState> playbackState,
        IMapper mapper)
    {
        this.logger = logger;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.state = state;
        this.playbackState = playbackState;
        this.mapper = mapper;
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

        // Prepare schedule state item BEFORE navigation
        // This ensures CurrentSchedule is set in state before the page initializes
        // The reducer will DeepClone it, so we don't need to clone here
        var sourceScheduleStateItem = GetSourceScheduleStateItem(scheduleListItem.Schedule.Id, scheduleViewModels);

        // Dispatch action to set CurrentSchedule BEFORE navigation
        // This prevents ScheduleStateManager from creating a sample schedule
        dispatcher.Dispatch(new ViewScheduleAction(sourceScheduleStateItem));

        // Navigate AFTER dispatching action so CurrentSchedule is already set when page initializes
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
}

