#nullable enable
using AutoMapper;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;

#if ANDROID
using Bible.Alarm.Platforms.Android.Services.Helpers;
#endif

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

public sealed class ScheduleCommandService : IScheduleCommandService
{
    private readonly ILogger logger;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IScheduleSaveService scheduleSaveService;
    private readonly IPlaybackService playbackService;
    private readonly INotificationService notificationService;
    private readonly IToastService toastService;
    private readonly IMapper mapper;
    private readonly IState<ApplicationState> state;

    public ScheduleCommandService(
        ILogger logger,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IScheduleSaveService scheduleSaveService,
        IPlaybackService playbackService,
        INotificationService notificationService,
        IToastService toastService,
        IMapper mapper,
        IState<ApplicationState> state)
    {
        this.logger = logger;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.scheduleSaveService = scheduleSaveService;
        this.playbackService = playbackService;
        this.notificationService = notificationService;
        this.toastService = toastService;
        this.mapper = mapper;
        this.state = state;
    }

    public async Task ExecuteCancelAsync(
        bool isNewSchedule,
        int scheduleId,
        ScheduleStateItem? currentSchedule)
    {
        logger.Information(AppConstants.Logging.ScheduleCommandDiagnosticsLog.CancelCommandCancelButtonClicked, scheduleId, isNewSchedule);

        // If it's a new schedule, remove it from state and navigate home
        if (isNewSchedule)
        {
            logger.Debug(AppConstants.Logging.ScheduleCommandDiagnosticsLog.CancelCommandNewScheduleRemovingFromStateNavigatingHome);

            // Remove any unsaved schedule (ID <= 0) from the Schedules collection
            var currentState = state.Value;
            if (currentState.Schedules != null)
            {
                foreach (var unsavedId in currentState.Schedules.Where(s => s.Id <= 0).Select(s => s.Id))
                {
                    logger.Debug(AppConstants.Logging.ScheduleCommandDiagnosticsLog.CancelCommandRemovingUnsavedScheduleFromState, unsavedId);
                    dispatcher.Dispatch(new RemoveScheduleSuccessAction(unsavedId));
                }
            }

            // Reset schedule state
            dispatcher.Dispatch(new ResetScheduleStateAction());

            // Hide overlay when navigating away
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });

            await navigationService.NavigateToHomeAsync();
            return;
        }

        // Existing schedule cancel:
        // Unsaved edits are applied only to CurrentSchedule (ShouldSave=false), not to the persisted Schedules list.
        // So we can safely discard the draft by resetting state and navigating home without any DB calls.
        logger.Debug(AppConstants.Logging.ScheduleCommandDiagnosticsLog.CancelCommandExistingScheduleDiscardingDraftNoDbReload, scheduleId);

        // Clear CurrentSchedule after cancel (discard draft changes)
        dispatcher.Dispatch(new ResetScheduleStateAction());

        // Hide overlay when navigating away
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });

        // Navigate to home
        await navigationService.NavigateToHomeAsync();
    }

    public async Task<bool> ExecuteSaveAsync(
        bool isNewSchedule,
        int scheduleId,
        ScheduleStateItem currentSchedule,
        bool musicUpdated,
        bool biblePublicationUpdated,
        bool modelInitialized)
    {
        logger.Information(AppConstants.Logging.ScheduleCommandDiagnosticsLog.SaveCommandSaveButtonClicked,
            isNewSchedule, scheduleId, currentSchedule.Name);

        if (!modelInitialized)
        {
            logger.Error(AppConstants.Logging.ScheduleCommandDiagnosticsLog.SaveAsyncModelNotInitializedCannotSave);
            await toastService.ShowMessage(AppConstants.ToastMessages.ScheduleDataNotReadyTryAgain);
            return false;
        }

        if (!isNewSchedule && scheduleId <= 0)
        {
            logger.Error(AppConstants.Logging.ScheduleCommandDiagnosticsLog.SaveAsyncInvalidScheduleIdForExistingSchedule, scheduleId);
            await toastService.ShowMessage(AppConstants.ToastMessages.InvalidScheduleIdTryAgain);
            return false;
        }

        // Validate WeekDays before saving
        if (currentSchedule.DaysOfWeek == 0)
        {
            logger.Warning(AppConstants.Logging.ScheduleCommandDiagnosticsLog.SaveCommandCannotSaveDaysOfWeekEmpty);
            await toastService.ShowMessage(AppConstants.ToastMessages.SelectAtLeastOneDay, 5);
            return false;
        }

        // Check notification permission if NotificationEnabled is true (Android 13+)
        if (currentSchedule.NotificationEnabled)
        {
#if ANDROID
            // Check permission status without waiting (non-blocking)
            // Permission requests are handled by ViewModels via the modal
            var granted = NotificationPermissionHelper.IsNotificationPermissionGranted();
            if (!granted)
            {
                logger.Warning(AppConstants.Logging.ScheduleCommandDiagnosticsLog.CannotSaveScheduleNotificationEnabledPermissionDenied);
                return false;
            }
#endif
        }

        // For new schedules, ensure IsEnabled is true in state
        if (isNewSchedule && !currentSchedule.IsEnabled)
        {
            var updatedSchedule = mapper.Map<ScheduleStateItem>(currentSchedule);
            updatedSchedule.IsEnabled = true;
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
            await Task.Delay(50);
        }

        logger.Debug(AppConstants.Logging.ScheduleCommandDiagnosticsLog.ExecuteSaveAsyncBeforePrepareModelForSaveTracksAndAlwaysPlayFromStart,
            currentSchedule.NumberOfTracksToPlay, currentSchedule.AlwaysPlayFromStart);
        
        var model = await scheduleSaveService.PrepareModelForSaveAsync(currentSchedule, isNewSchedule, musicUpdated);
        var scheduleStateItem = scheduleSaveService.PrepareScheduleStateItem(model, currentSchedule, musicUpdated);

        // IMPORTANT: Reset progress only on Save, and only if the saved track identity changed.
        if (biblePublicationUpdated)
        {
            scheduleStateItem.BiblePublicationFinishedDuration = TimeSpan.Zero;
        }

        logger.Debug(AppConstants.Logging.ScheduleCommandDiagnosticsLog.ExecuteSaveAsyncAfterPrepareScheduleStateItemTracksAndAlwaysPlayFromStart,
            scheduleStateItem.NumberOfTracksToPlay, scheduleStateItem.AlwaysPlayFromStart);

        if (isNewSchedule)
        {
            logger.Information(AppConstants.Logging.ScheduleCommandDiagnosticsLog.DispatchSaveActionDispatchingCreateScheduleAction);

            // Capture the current saved schedule count before dispatching
            var previousSavedCount = state.Value.Schedules?.Count(s => s.Id > 0) ?? 0;

            dispatcher.Dispatch(new CreateScheduleAction(scheduleStateItem, musicUpdated, biblePublicationUpdated));

            // Wait for the Fluxor effect to complete and the new schedule to appear in state.
            // This keeps the save overlay visible on the schedule page until the schedule is ready,
            // so when we navigate to home the schedule is already in the list (no delay / missing row).
            await WaitForNewScheduleInStateAsync(previousSavedCount);
        }
        else
        {
            logger.Information(AppConstants.Logging.ScheduleCommandDiagnosticsLog.DispatchSaveActionDispatchingUpdateScheduleFromViewModelShouldSaveTrue,
                scheduleStateItem.NumberOfTracksToPlay, scheduleStateItem.AlwaysPlayFromStart);
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(scheduleStateItem, musicUpdated, biblePublicationUpdated, shouldSave: true));
        }

        return true;
    }

    private async Task WaitForNewScheduleInStateAsync(int previousSavedCount)
    {
        var tcs = new TaskCompletionSource<bool>();

        void OnStateChanged(object? sender, EventArgs e)
        {
            var schedules = state.Value.Schedules;
            if (schedules == null)
            {
                return;
            }

            var currentSavedCount = schedules.Count(s => s.Id > 0);
            if (currentSavedCount > previousSavedCount)
            {
                tcs.TrySetResult(true);
            }
        }

        state.StateChanged += OnStateChanged;

        try
        {
            // Check immediately in case the effect already completed
            OnStateChanged(null, EventArgs.Empty);

            // Wait with a 10-second timeout to avoid hanging forever if something goes wrong
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(10000));

            if (completed != tcs.Task)
            {
                logger.Warning(AppConstants.Logging.ScheduleCommandDiagnosticsLog.WaitForNewScheduleInStateTimedOut);
            }
        }
        finally
        {
            state.StateChanged -= OnStateChanged;
        }
    }

    public async Task<bool> ExecuteDeleteAsync(
        bool isNewSchedule,
        int scheduleId,
        int scheduleCount)
    {
        if (isNewSchedule)
        {
            // Pop without animation to prevent the overlay from being visible
            // during the page transition
            await navigationService.NavigateToHomeAsync(animated: false);
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
            return true;
        }

        if (scheduleId <= 0)
        {
            // Hide overlay on invalid schedule ID to prevent infinite spinner
            logger.Warning(AppConstants.Logging.ScheduleCommandDiagnosticsLog.ExecuteDeleteAsyncInvalidScheduleIdHidingOverlay, scheduleId);
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
            return false;
        }

        // Check if this is the last schedule - prevent deletion if it is
        if (scheduleCount <= 1)
        {
            logger.Warning(AppConstants.Logging.ScheduleCommandDiagnosticsLog.CannotDeleteScheduleLastRemaining, scheduleId);
            // Hide overlay BEFORE showing toast (toast is awaited and blocks until dismissed)
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
            await toastService.ShowMessage(AppConstants.ToastMessages.CannotDeleteLastSchedule);
            return false;
        }

        logger.Information(AppConstants.Logging.ScheduleCommandDiagnosticsLog.DeleteAsyncDispatchingDeleteScheduleAction, scheduleId);
        dispatcher.Dispatch(new DeleteScheduleAction(scheduleId));

        // Clear CurrentSchedule after delete operation
        dispatcher.Dispatch(new ResetScheduleStateAction());

        // Pop without animation to prevent the overlay from being visible
        // during the page transition
        await navigationService.NavigateToHomeAsync(animated: false);

        // Clean up stale overlay state now that the schedule page is gone
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
        return true;
    }

    public async Task ValidateNotificationPermissionsAsync(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule != null &&
            currentSchedule.IsEnabled &&
            (DeviceInfo.Platform == DevicePlatform.iOS || DeviceInfo.Platform == DevicePlatform.WinUI) &&
            !await notificationService.CanScheduleAsync())
        {
            var updatedSchedule = mapper.Map<ScheduleStateItem>(currentSchedule);
            updatedSchedule.IsEnabled = false;
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
        }
    }

    public async Task StopPlaybackIfNeededAsync(
        bool isNewSchedule,
        bool isPreparingOrPlaying,
        int scheduleId,
        int currentPlaybackScheduleId)
    {
        if (!isNewSchedule &&
            isPreparingOrPlaying &&
            scheduleId == currentPlaybackScheduleId)
        {
            await playbackService.StopAsync();
        }
    }

    public async Task HandleSaveResultAsync(
        bool saved,
        int scheduleId,
        bool isEnabled,
        AlarmSchedule model)
    {
        if (saved)
        {
            logger.Information(AppConstants.Logging.ScheduleCommandDiagnosticsLog.SaveCommandSaveSuccessfulNavigatingHome, scheduleId);

            // Clear CurrentSchedule after successful save
            dispatcher.Dispatch(new ResetScheduleStateAction());

            // Pop without animation to prevent the overlay from being visible
            // during the page transition (overlay sliding over the home page)
            await navigationService.NavigateToHomeAsync(animated: false);

            // Clean up stale overlay state now that the schedule page is gone
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
        }
        else
        {
            logger.Error(AppConstants.Logging.ScheduleCommandDiagnosticsLog.SaveCommandSaveFailedHidingOverlay, scheduleId);
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
        }

        if (saved && isEnabled)
        {
            await toastService.ShowScheduledNotification(model);
        }
        else if (saved)
        {
            await toastService.ShowMessage(AppConstants.ToastMessages.ScheduleSaved);
        }
    }
}

