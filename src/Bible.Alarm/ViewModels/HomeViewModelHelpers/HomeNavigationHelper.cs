#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AutoMapper;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.General;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IPlaybackModalService playbackModalService;
    private readonly IState<PlaybackState> playbackState;
    private readonly IServiceProvider serviceProvider;
    private readonly IMapper mapper;
    private readonly Dictionary<int, DateTime> recentPlayClicks = new();
    private const int PlayClickCooldownMs = 500;

    public Action<int> TrackPlayClick => (scheduleId) => recentPlayClicks[scheduleId] = DateTime.UtcNow;

    public HomeNavigationHelper(
        ILogger logger,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IPlaybackModalService playbackModalService,
        IState<PlaybackState> playbackState,
        IServiceProvider serviceProvider,
        IMapper mapper)
    {
        this.logger = logger;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.playbackModalService = playbackModalService;
        this.playbackState = playbackState;
        this.serviceProvider = serviceProvider;
        this.mapper = mapper;
    }

    public bool ShouldSkipNavigation(int scheduleId)
    {
        var currentPlaybackState = playbackState.Value;
        if (currentPlaybackState.IsPreparingOrPlaying &&
            currentPlaybackState.CurrentScheduleId == scheduleId &&
            navigationService.IsPlaybackModalOnScreen())
        {
            logger.Debug("ViewScheduleCommand: Skipping navigation - playback modal is on screen for schedule {ScheduleId}", scheduleId);
            return true;
        }

        if (playbackModalService.WasRecentlyMinimized())
        {
            logger.Debug("ViewScheduleCommand: Skipping navigation - playback was recently minimized (ghost tap guard)");
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

        var schedule = scheduleListItem.Schedule!;
        var reminderEvaluation = await EvaluateReminderAndPermissionModalAsync(schedule, scheduleListItem.IsEnabled);

        await navigationService.NavigateToScheduleAsync(schedule.Id, reminderEvaluation.ShouldEnableReminder);

        await PresentNotificationPermissionModalAfterNavigateAsync(schedule, reminderEvaluation.ShouldShowPermissionModal);

#if DEBUG
        var endTime = DateTime.UtcNow;
        logger.Information("[PERF] ShowOverlayAndNavigateAsync: Complete in {ElapsedMs}ms",
            (endTime - startTime).TotalMilliseconds);
#endif
    }

    private async Task<(bool ShouldEnableReminder, bool ShouldShowPermissionModal)> EvaluateReminderAndPermissionModalAsync(
        AlarmSchedule schedule,
        bool initialShouldEnableReminder)
    {
#if ANDROID
        return await Task.FromResult(EvaluateAndroidReminderAndModal(schedule, initialShouldEnableReminder));
#elif IOS
        return await EvaluateIosReminderAndModalAsync(schedule, initialShouldEnableReminder);
#else
        return (initialShouldEnableReminder, false);
#endif
    }

#if ANDROID
    private (bool ShouldEnableReminder, bool ShouldShowPermissionModal) EvaluateAndroidReminderAndModal(
        AlarmSchedule schedule,
        bool shouldEnableReminder)
    {
        var shouldShowPermissionModal = false;
        if (!schedule.NotificationEnabled)
        {
            return (shouldEnableReminder, shouldShowPermissionModal);
        }

        try
        {
            var permissionService = NotificationPermissionService.Instance;
            bool isPermissionGranted;
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

        return (shouldEnableReminder, shouldShowPermissionModal);
    }
#endif

#if IOS
    private async Task<(bool ShouldEnableReminder, bool ShouldShowPermissionModal)> EvaluateIosReminderAndModalAsync(
        AlarmSchedule schedule,
        bool shouldEnableReminder)
    {
        var shouldShowPermissionModal = false;
        if (!schedule.IsEnabled)
        {
            return (shouldEnableReminder, shouldShowPermissionModal);
        }

        try
        {
            bool isPermissionGranted;
            try
            {
                isPermissionGranted = await IosNotificationPermissionService.IsGrantedAsync();
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

        return (shouldEnableReminder, shouldShowPermissionModal);
    }
#endif

    private async Task PresentNotificationPermissionModalAfterNavigateAsync(
        AlarmSchedule schedule,
        bool shouldShowPermissionModal)
    {
        if (!shouldShowPermissionModal)
        {
            return;
        }

        await Task.Delay(1000);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var notificationViewModel = new NotificationPermissionViewModel(
                    serviceProvider.GetRequiredService<ILogger>(),
                    navigationService,
                    onModalDismissed: (permissionGranted) =>
                        NotificationPermissionModalDismissed(schedule, permissionGranted));

                notificationViewModel.StartPermissionCheckTimer();
                await navigationService.OpenNotificationPermissionModalAsync(notificationViewModel);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "ShowOverlayAndNavigateAsync: Error opening notification permission modal");
            }
        });
    }

    private void NotificationPermissionModalDismissed(AlarmSchedule schedule, bool permissionGranted)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var state = serviceProvider.GetRequiredService<IState<ApplicationState>>();
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

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(100);
#if ANDROID
                RunAndroidModalDismissStateUpdate(schedule, permissionGranted);
#elif IOS
                await RunIosModalDismissDbAndStateUpdateAsync(schedule, permissionGranted);
#else
                await Task.CompletedTask;
#endif
            }
            catch (Exception ex)
            {
                logger.Error(ex, "ShowOverlayAndNavigateAsync: Error in onModalDismissed callback");
            }
        });
    }

#if ANDROID
    private void RunAndroidModalDismissStateUpdate(AlarmSchedule schedule, bool permissionGranted)
    {
        var dbDispatcher = serviceProvider.GetRequiredService<IDispatcher>();
        var state = serviceProvider.GetRequiredService<IState<ApplicationState>>();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var schedules = state.Value.Schedules;
                var scheduleToUpdate = schedules?.FirstOrDefault(s => s.Id == schedule.Id);

                if (scheduleToUpdate != null)
                {
                    var updatedSchedule = mapper.Map<ScheduleStateItem>(scheduleToUpdate);
                    updatedSchedule.NotificationEnabled = permissionGranted;
                    dbDispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
                    logger.Information("ShowOverlayAndNavigateAsync: Permission {PermissionStatus} from modal - set NotificationEnabled to {NotificationEnabled} for schedule {ScheduleId}",
                        permissionGranted ? "granted" : "denied", permissionGranted, schedule.Id);
                }
            }
            catch (Exception stateEx)
            {
                logger.Error(stateEx, "ShowOverlayAndNavigateAsync: Error updating NotificationEnabled in state");
            }
        });
    }
#endif

#if IOS
    private async Task RunIosModalDismissDbAndStateUpdateAsync(AlarmSchedule schedule, bool permissionGranted)
    {
        var dbDispatcher = serviceProvider.GetRequiredService<IDispatcher>();
        var alarmScheduleService = serviceProvider.GetRequiredService<IAlarmScheduleService>();
        var state = serviceProvider.GetRequiredService<IState<ApplicationState>>();
        try
        {
            _ = await alarmScheduleService.UpdateScheduleByIdAsync(
                schedule.Id,
                s => s.IsEnabled = permissionGranted,
                CancellationToken.None);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    var schedules = state.Value.Schedules;
                    var scheduleToUpdate = schedules?.FirstOrDefault(s => s.Id == schedule.Id);

                    if (scheduleToUpdate != null)
                    {
                        var stateUpdatedSchedule = mapper.Map<ScheduleStateItem>(scheduleToUpdate);
                        stateUpdatedSchedule.IsEnabled = permissionGranted;
                        dbDispatcher.Dispatch(new UpdateScheduleFromViewModelAction(stateUpdatedSchedule, false, false, shouldSave: false));
                    }

                    logger.Information("ShowOverlayAndNavigateAsync: Permission {PermissionStatus} from modal - set IsEnabled to {IsEnabled} in DB for schedule {ScheduleId}",
                        permissionGranted ? "granted" : "denied", permissionGranted, schedule.Id);
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
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    var schedules = state.Value.Schedules;
                    var scheduleToUpdate = schedules?.FirstOrDefault(s => s.Id == schedule.Id);

                    if (scheduleToUpdate != null)
                    {
                        var updatedSchedule = mapper.Map<ScheduleStateItem>(scheduleToUpdate);
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
    }
#endif
}

