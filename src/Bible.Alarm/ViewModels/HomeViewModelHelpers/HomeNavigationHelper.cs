#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
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
    private readonly IState<PlaybackState> playbackState;
    private readonly Dictionary<int, DateTime> recentPlayClicks = new();
    private const int PlayClickCooldownMs = 500;

    public Action<int> TrackPlayClick => (scheduleId) => recentPlayClicks[scheduleId] = DateTime.UtcNow;

    public HomeNavigationHelper(
        ILogger logger,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IState<PlaybackState> playbackState)
    {
        this.logger = logger;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.playbackState = playbackState;
    }

    public bool ShouldSkipNavigation(int scheduleId)
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

    public async Task ShowOverlayAndNavigateAsync(ScheduleListItemViewModel scheduleListItem)
    {
        if (scheduleListItem.Schedule == null)
        {
            return;
        }

#if DEBUG
        var startTime = DateTime.UtcNow;
        logger.Information("[PERF] ShowOverlayAndNavigateAsync: Start at {StartTime}, ScheduleId={ScheduleId}",
            startTime, scheduleListItem.Schedule.Id);
#endif

        // Show overlay IMMEDIATELY for instant feedback (like Add button)
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = true });

#if DEBUG
        logger.Information("[PERF] ShowOverlayAndNavigateAsync: Overlay dispatched, now calling NavigateToScheduleAsync");
#endif

        // Navigate with the schedule ID - state will be set inside the lock to prevent race conditions
        // (If we set state here and wait for lock, Home.OnAppearing could clear it)
        await navigationService.NavigateToScheduleAsync(scheduleListItem.Schedule.Id, scheduleListItem.IsEnabled);

#if DEBUG
        var endTime = DateTime.UtcNow;
        logger.Information("[PERF] ShowOverlayAndNavigateAsync: Complete in {ElapsedMs}ms",
            (endTime - startTime).TotalMilliseconds);
#endif
    }
}

