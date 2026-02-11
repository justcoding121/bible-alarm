#nullable enable
using System.Diagnostics.CodeAnalysis;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.ViewModels.General;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Fluxor;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using System.Threading;
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

    [SuppressMessage("SonarAnalyzer.CSharp", "S2583", Justification = "shouldShowPermissionModal is set in platform-specific #if ANDROID/#elif IOS blocks.")]
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

#if ANDROID
        // Android: Check if schedule has NotificationEnabled=true but permission is not granted
        // If so, enable reminder and show permission modal after navigation
        if (schedule.NotificationEnabled)
        {
            try
            {
                var permissionService = NotificationPermissionService.Instance;
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
#elif IOS
        // iOS: Check if schedule has IsEnabled=true but permission is not granted
        // If so, enable reminder and show permission modal after navigation
        // iOS works directly with IsEnabled (reminder enabled), not NotificationEnabled
        if (schedule.IsEnabled)
        {
            try
            {
                var permissionService = IOSNotificationPermissionService.Instance;
                bool isPermissionGranted = false;
                try
                {
                    // Use async check to get accurate real-time status.
                    // The sync IsGranted property can return false when cache is empty
                    // (e.g., on first app launch before the async check completes),
                    // which would unnecessarily show the permission modal.
                    isPermissionGranted = await permissionService.IsGrantedAsync();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "ShowOverlayAndNavigateAsync: Exception checking permission - assuming not granted");
                    isPermissionGranted = false;
                }

                if (!isPermissionGranted)
                {
                    logger.Information("ShowOverlayAndNavigateAsync: Schedule {ScheduleId} has IsEnabled=true but permission not granted - will enable reminder and show modal", schedule.Id);
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

        // Show notification permission modal after navigation if needed (shouldShowPermissionModal set in #if ANDROID / #elif IOS).
        if (shouldShowPermissionModal)
        {
            // Wait longer for the schedule page to fully initialize and containers to signal ready
            // This ensures the overlay can be properly hidden after modal dismissal
            await Task.Delay(1000);
            
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
                            // Ensure overlay is hidden after modal dismissal
                            // The schedule page containers should have signaled ready by now
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                try
                                {
                                    var state = serviceProvider.GetRequiredService<IState<ApplicationState>>();
                                    // If containers are ready, hide overlay immediately
                                    // Otherwise, let the normal flow handle it
                                    if (state.Value.ContainerReadiness.AllReady && state.Value.IsSchedulePageOverlayVisible)
                                    {
                                        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
                                        logger.Debug("ShowOverlayAndNavigateAsync: Hiding overlay after modal dismissal - containers are ready");
                                    }
                                }
                                catch (Exception overlayEx)
                                {
                                    logger.Error(overlayEx, "ShowOverlayAndNavigateAsync: Error checking/hiding overlay after modal dismissal");
                                }
                            });
                            
                            // Run DB update in background to avoid blocking modal dismissal
                            // Use fire-and-forget pattern with proper error handling
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await Task.Delay(100); // Brief delay to ensure modal is fully dismissed
                                    
                                    // Get services on background thread
                                    var dbDispatcher = serviceProvider.GetRequiredService<IDispatcher>();
                                    var alarmScheduleService = serviceProvider.GetRequiredService<IAlarmScheduleService>();
                                    var state = serviceProvider.GetRequiredService<IState<ApplicationState>>();
                                    
#if ANDROID
                                    // Android: Update NotificationEnabled in state only (not DB)
                                    MainThread.BeginInvokeOnMainThread(() =>
                                    {
                                        try
                                        {
                                            var schedules = state.Value.Schedules;
                                            var scheduleToUpdate = schedules?.FirstOrDefault(s => s.Id == scheduleId);
                                            
                                            if (scheduleToUpdate != null)
                                            {
                                                var updatedSchedule = ScheduleStateHelper.CloneScheduleStateItem(scheduleToUpdate);
                                                updatedSchedule.NotificationEnabled = permissionGranted;
                                                dbDispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
                                                logger.Information("ShowOverlayAndNavigateAsync: Permission {PermissionStatus} from modal - set NotificationEnabled to {NotificationEnabled} for schedule {ScheduleId}", 
                                                    permissionGranted ? "granted" : "denied", permissionGranted, scheduleId);
                                            }
                                        }
                                        catch (Exception stateEx)
                                        {
                                            logger.Error(stateEx, "ShowOverlayAndNavigateAsync: Error updating NotificationEnabled in state");
                                        }
                                    });
#elif IOS
                                    // iOS: Update IsEnabled in DB (like ScheduleStateService does)
                                    try
                                    {
                                        var dbUpdatedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
                                            scheduleId,
                                            s => s.IsEnabled = permissionGranted,
                                            CancellationToken.None);
                                        
                                        // Update Fluxor store with DB value on main thread
                                        MainThread.BeginInvokeOnMainThread(() =>
                                        {
                                            try
                                            {
                                                var schedules = state.Value.Schedules;
                                                var scheduleToUpdate = schedules?.FirstOrDefault(s => s.Id == scheduleId);
                                
                                                if (scheduleToUpdate != null)
                                                {
                                                    var stateUpdatedSchedule = ScheduleStateHelper.CloneScheduleStateItem(scheduleToUpdate);
                                                    stateUpdatedSchedule.IsEnabled = permissionGranted;
                                                    dbDispatcher.Dispatch(new UpdateScheduleFromViewModelAction(stateUpdatedSchedule, false, false, shouldSave: false));
                                                }
                                
                                                logger.Information("ShowOverlayAndNavigateAsync: Permission {PermissionStatus} from modal - set IsEnabled to {IsEnabled} in DB for schedule {ScheduleId}", 
                                                    permissionGranted ? "granted" : "denied", permissionGranted, scheduleId);
                                            }
                                            catch (Exception stateEx)
                                            {
                                                logger.Error(stateEx, "ShowOverlayAndNavigateAsync: Error updating state after DB update");
                                            }
                                        });
                                    }
                                    catch (Exception dbEx)
                                    {
                                        logger.Error(dbEx, "ShowOverlayAndNavigateAsync: Error updating IsEnabled in DB - falling back to state update");
                                        // Fallback: update state only if DB update fails
                                        MainThread.BeginInvokeOnMainThread(() =>
                                        {
                                            try
                                            {
                                                var schedules = state.Value.Schedules;
                                                var scheduleToUpdate = schedules?.FirstOrDefault(s => s.Id == scheduleId);
                                                
                                                if (scheduleToUpdate != null)
                                                {
                                                    var updatedSchedule = ScheduleStateHelper.CloneScheduleStateItem(scheduleToUpdate);
                                                    updatedSchedule.IsEnabled = permissionGranted;
                                                    dbDispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
                                                }
                                            }
                                            catch (Exception fallbackEx)
                                            {
                                                logger.Error(fallbackEx, "ShowOverlayAndNavigateAsync: Error in fallback state update");
                                            }
                                        });
                                    }
#endif
                                }
                                catch (Exception ex)
                                {
                                    logger.Error(ex, "ShowOverlayAndNavigateAsync: Error in onModalDismissed callback");
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

