#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.ViewModels.General;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Fluxor;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
#if ANDROID
using Bible.Alarm.Platforms.Android.Services.Helpers;
#elif IOS
using Bible.Alarm.Platforms.iOS.Services.Helpers;
#endif

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
    private readonly IServiceProvider serviceProvider;
    private readonly Dictionary<int, DateTime> recentPlayClicks = new();
    private const int PlayClickCooldownMs = 500;

    public Action<int> TrackPlayClick => (scheduleId) => recentPlayClicks[scheduleId] = DateTime.UtcNow;

    public HomeNavigationHelper(
        ILogger logger,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IState<PlaybackState> playbackState,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.playbackState = playbackState;
        this.serviceProvider = serviceProvider;
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

        var schedule = scheduleListItem.Schedule;
        var shouldEnableReminder = scheduleListItem.IsEnabled;
        var shouldShowPermissionModal = false;

#if ANDROID || IOS
        // Check if schedule has NotificationEnabled=true but permission is not granted
        // If so, enable reminder and show permission modal after navigation
        if (schedule.NotificationEnabled)
        {
            try
            {
#if ANDROID
                var permissionService = NotificationPermissionService.Instance;
#elif IOS
                var permissionService = IOSNotificationPermissionService.Instance;
#endif
                bool isPermissionGranted = false;
                try
                {
                    isPermissionGranted = permissionService.IsGranted;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "ShowOverlayAndNavigateAsync: Exception checking permission - assuming not granted");
                    isPermissionGranted = false;
                }

                if (!isPermissionGranted)
                {
                    logger.Information("ShowOverlayAndNavigateAsync: Schedule {ScheduleId} has NotificationEnabled=true but permission not granted - will enable reminder and show modal", schedule.Id);
                    shouldEnableReminder = true;
                    shouldShowPermissionModal = true;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "ShowOverlayAndNavigateAsync: Exception checking notification permission");
            }
        }
#endif

        // Navigate with the schedule ID - state will be set inside the lock to prevent race conditions
        // (If we set state here and wait for lock, Home.OnAppearing could clear it)
        await navigationService.NavigateToScheduleAsync(schedule.Id, shouldEnableReminder);

        // Show notification permission modal after navigation if needed
        if (shouldShowPermissionModal)
        {
            // Wait a bit for the schedule page to initialize
            await Task.Delay(500);
            
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var scheduleId = schedule.Id;
                    var notificationViewModel = new NotificationPermissionViewModel(
                        serviceProvider.GetRequiredService<ILogger>(),
                        navigationService,
                        serviceProvider,
                        onModalDismissed: (permissionGranted) =>
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                try
                                {
                                    // Update NotificationEnabled based on permission status
                                    // Get current schedule from state and update NotificationEnabled
                                    var state = serviceProvider.GetRequiredService<IState<ApplicationState>>();
                                    var currentSchedule = state.Value.CurrentSchedule;
                                    
                                    if (currentSchedule != null && currentSchedule.Id == scheduleId)
                                    {
                                        // Clone schedule and set NotificationEnabled based on permission
                                        var updatedSchedule = ScheduleStateHelper.CloneScheduleStateItem(currentSchedule);
                                        updatedSchedule.NotificationEnabled = permissionGranted;
                                        
                                        // Dispatch update action to set NotificationEnabled
                                        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
                                        logger.Information("ShowOverlayAndNavigateAsync: Permission {PermissionStatus} from modal - set NotificationEnabled to {NotificationEnabled} for schedule {ScheduleId}", 
                                            permissionGranted ? "granted" : "denied", permissionGranted, scheduleId);
                                    }
                                    else
                                    {
                                        logger.Warning("ShowOverlayAndNavigateAsync: CurrentSchedule not found or ID mismatch when trying to set NotificationEnabled. ScheduleId={ScheduleId}, CurrentScheduleId={CurrentScheduleId}", 
                                            scheduleId, currentSchedule?.Id ?? 0);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logger.Error(ex, "ShowOverlayAndNavigateAsync: Error setting NotificationEnabled after permission check");
                                }
                            });
                        });
                    
                    notificationViewModel.StartPermissionCheckTimer();
                    await navigationService.OpenNotificationPermissionModalAsync(notificationViewModel);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "ShowOverlayAndNavigateAsync: Error opening notification permission modal");
                }
            });
        }

#if DEBUG
        var endTime = DateTime.UtcNow;
        logger.Information("[PERF] ShowOverlayAndNavigateAsync: Complete in {ElapsedMs}ms",
            (endTime - startTime).TotalMilliseconds);
#endif
    }
}

