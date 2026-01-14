#nullable enable
using AutoMapper;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
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
    private readonly IAlarmScheduleService alarmScheduleService;
    private readonly IScheduleDisplayNameService scheduleDisplayNameService;
    private readonly IScheduleSaveService scheduleSaveService;
    private readonly IScheduleValidationService scheduleValidationService;
    private readonly IPlaybackService playbackService;
    private readonly INotificationService notificationService;
    private readonly IToastService toastService;
    private readonly IMapper mapper;
    private readonly IState<ApplicationState> state;
    private readonly IState<PlaybackState> playbackState;

    public ScheduleCommandService(
        ILogger logger,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IAlarmScheduleService alarmScheduleService,
        IScheduleDisplayNameService scheduleDisplayNameService,
        IScheduleSaveService scheduleSaveService,
        IScheduleValidationService scheduleValidationService,
        IPlaybackService playbackService,
        INotificationService notificationService,
        IToastService toastService,
        IMapper mapper,
        IState<ApplicationState> state,
        IState<PlaybackState> playbackState)
    {
        this.logger = logger;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.alarmScheduleService = alarmScheduleService;
        this.scheduleDisplayNameService = scheduleDisplayNameService;
        this.scheduleSaveService = scheduleSaveService;
        this.scheduleValidationService = scheduleValidationService;
        this.playbackService = playbackService;
        this.notificationService = notificationService;
        this.toastService = toastService;
        this.mapper = mapper;
        this.state = state;
        this.playbackState = playbackState;
    }

    public async Task ExecuteCancelAsync(
        bool isNewSchedule,
        int scheduleId,
        ScheduleStateItem? currentSchedule)
    {
        logger.Information("CancelCommand: Cancel button clicked. ScheduleId={ScheduleId}, IsNewSchedule={IsNewSchedule}", scheduleId, isNewSchedule);

        // If it's a new schedule, remove it from state and navigate home
        if (isNewSchedule)
        {
            logger.Debug("CancelCommand: New schedule, removing from state and navigating to home");

            // Remove any unsaved schedule (ID <= 0) from the Schedules collection
            var currentState = state.Value;
            if (currentState.Schedules != null)
            {
                var unsavedSchedules = currentState.Schedules.Where(s => s.Id <= 0).ToList();
                foreach (var unsavedSchedule in unsavedSchedules)
                {
                    logger.Debug("CancelCommand: Removing unsaved schedule with ID {ScheduleId} from state", unsavedSchedule.Id);
                    dispatcher.Dispatch(new RemoveScheduleSuccessAction(unsavedSchedule.Id));
                }
            }

            // Reset schedule state
            dispatcher.Dispatch(new ResetScheduleStateAction());

            // Hide overlay when navigating away
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });

            await navigationService.NavigateToHomeAsync();
            return;
        }

        // For existing schedules, reload from database to revert optimistic changes
        if (scheduleId > 0)
        {
            logger.Debug("CancelCommand: Existing schedule, reloading from database to revert optimistic changes. ScheduleId={ScheduleId}", scheduleId);

            try
            {
                // Reload schedule from database
                var reloadedSchedule = await Task.Run(async () =>
                    await alarmScheduleService.GetScheduleByIdAsync(scheduleId, true, true, CancellationToken.None));

                if (reloadedSchedule != null)
                {
                    // Map to ScheduleStateItem and populate display names
                    var scheduleStateItem = mapper.Map<ScheduleStateItem>(reloadedSchedule);

                    // Populate display names (these are not stored in DB, need to be populated)
                    await scheduleDisplayNameService.PopulateDisplayNamesAsync(scheduleStateItem, reloadedSchedule);

                    // Update the schedule in the Schedules collection to revert optimistic changes
                    logger.Information("CancelCommand: Reloaded schedule from database. LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, SectionNumber: {SectionNumber}, TrackNumber: {TrackNumber}",
                        scheduleStateItem.BiblePublicationLanguageCode ?? "null",
                        scheduleStateItem.BiblePublicationCode ?? "null",
                        scheduleStateItem.BiblePublicationSectionNumber ?? 0,
                        scheduleStateItem.BiblePublicationTrackNumber ?? 0);

                    // Dispatch action to update the schedule in Schedules collection
                    dispatcher.Dispatch(new UpdateScheduleSuccessAction(scheduleStateItem));
                }
                else
                {
                    logger.Warning("CancelCommand: Failed to reload schedule from database. ScheduleId={ScheduleId}", scheduleId);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "CancelCommand: Error reloading schedule from database. ScheduleId={ScheduleId}", scheduleId);
            }
        }

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
        logger.Information("SaveCommand: Save button clicked. IsNewSchedule={IsNewSchedule}, ScheduleId={ScheduleId}, Name={Name}",
            isNewSchedule, scheduleId, currentSchedule.Name);

        if (!modelInitialized)
        {
            logger.Error("SaveAsync: Model not initialized. Cannot save.");
            await toastService.ShowMessage("Schedule data is not ready. Please try again.");
            return false;
        }

        if (currentSchedule == null)
        {
            logger.Error("SaveAsync: CurrentSchedule is null. Cannot save.");
            await toastService.ShowMessage("Schedule data is missing. Please try again.");
            return false;
        }

        if (!isNewSchedule && scheduleId <= 0)
        {
            logger.Error("SaveAsync: Invalid ScheduleId for existing schedule. ScheduleId={ScheduleId}", scheduleId);
            await toastService.ShowMessage("Invalid schedule ID. Please try again.");
            return false;
        }

        var daysOfWeek = currentSchedule.DaysOfWeek;
        if (!await scheduleValidationService.ValidateDaysOfWeekAsync(daysOfWeek))
        {
            logger.Warning("SaveAsync: Validation failed");
            return false;
        }

        // Check notification permission if NotificationEnabled is true (Android 13+)
        if (currentSchedule.NotificationEnabled)
        {
#if ANDROID
            var granted = await NotificationPermissionHelper.RequestNotificationPermissionIfNeededAsync();
            if (!granted)
            {
                logger.Warning("Cannot save schedule with NotificationEnabled=true - notification permission denied");
                await toastService.ShowMessage(
                    "Notification permission is required for tap-to-play alarms. Please enable notifications in system settings.",
                    7);
                return false;
            }
#endif
        }

        // For new schedules, ensure IsEnabled is true in state
        if (isNewSchedule && !currentSchedule.IsEnabled)
        {
            var updatedSchedule = ScheduleStateHelper.CloneScheduleStateItem(currentSchedule);
            updatedSchedule.IsEnabled = true;
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
            await Task.Delay(50);
        }

        logger.Information("ExecuteSaveAsync: Before PrepareModelForSave - currentSchedule.NumberOfTracksToRead={NumberOfTracksToRead}, currentSchedule.AlwaysPlayFromStart={AlwaysPlayFromStart}",
            currentSchedule.NumberOfTracksToRead, currentSchedule.AlwaysPlayFromStart);
        
        var model = scheduleSaveService.PrepareModelForSave(currentSchedule, isNewSchedule, musicUpdated);
        var scheduleStateItem = scheduleSaveService.PrepareScheduleStateItem(model, currentSchedule, musicUpdated);

        logger.Information("ExecuteSaveAsync: After PrepareScheduleStateItem - scheduleStateItem.NumberOfTracksToRead={NumberOfTracksToRead}, scheduleStateItem.AlwaysPlayFromStart={AlwaysPlayFromStart}",
            scheduleStateItem.NumberOfTracksToRead, scheduleStateItem.AlwaysPlayFromStart);

        if (isNewSchedule)
        {
            logger.Information("DispatchSaveActionAsync: Dispatching CreateScheduleAction");
            dispatcher.Dispatch(new CreateScheduleAction(scheduleStateItem, musicUpdated, biblePublicationUpdated));
        }
        else
        {
            logger.Information("DispatchSaveActionAsync: Dispatching UpdateScheduleFromViewModelAction with shouldSave: true. scheduleStateItem.NumberOfTracksToRead={NumberOfTracksToRead}, scheduleStateItem.AlwaysPlayFromStart={AlwaysPlayFromStart}",
                scheduleStateItem.NumberOfTracksToRead, scheduleStateItem.AlwaysPlayFromStart);
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(scheduleStateItem, musicUpdated, biblePublicationUpdated, shouldSave: true));
        }

        return true;
    }

    public async Task<bool> ExecuteDeleteAsync(
        bool isNewSchedule,
        int scheduleId,
        int scheduleCount)
    {
        if (isNewSchedule)
        {
            // Keep overlay visible until page is destroyed by navigation
            // The overlay will be hidden when the page is destroyed
            await navigationService.NavigateToHomeAsync();
            return true;
        }

        if (scheduleId <= 0)
        {
            // Hide overlay on invalid schedule ID to prevent infinite spinner
            logger.Warning("ExecuteDeleteAsync: Invalid ScheduleId {ScheduleId}, hiding overlay", scheduleId);
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
            return false;
        }

        // Check if this is the last schedule - prevent deletion if it is
        if (scheduleCount <= 1)
        {
            logger.Warning("Cannot delete schedule {ScheduleId} - it is the last schedule", scheduleId);
            // Hide overlay BEFORE showing toast (toast is awaited and blocks until dismissed)
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
            await toastService.ShowMessage("Cannot delete last schedule");
            return false;
        }

        logger.Information("DeleteAsync: Dispatching DeleteScheduleAction for ScheduleId={ScheduleId}", scheduleId);
        dispatcher.Dispatch(new DeleteScheduleAction(scheduleId));

        // Clear CurrentSchedule after delete operation
        dispatcher.Dispatch(new ResetScheduleStateAction());

        // Keep overlay visible until page is destroyed by navigation
        // The overlay will be hidden when the page is destroyed
        await Task.Delay(100);
        await navigationService.NavigateToHomeAsync();
        return true;
    }

    public async Task ValidateNotificationPermissionsAsync(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule != null &&
            currentSchedule.IsEnabled &&
            (DeviceInfo.Platform == DevicePlatform.iOS || DeviceInfo.Platform == DevicePlatform.WinUI) &&
            !await notificationService.CanScheduleAsync())
        {
            // Update state to disable notifications
            var updatedSchedule = ScheduleStateHelper.CloneScheduleStateItem(currentSchedule);
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
            logger.Information("SaveCommand: Save successful, navigating to home. ScheduleId={ScheduleId}", scheduleId);

            // Clear CurrentSchedule after successful save
            dispatcher.Dispatch(new ResetScheduleStateAction());

            // Keep overlay visible until page is destroyed by navigation
            // The overlay will be hidden when the page is destroyed
            await Task.Delay(100);
            await navigationService.NavigateToHomeAsync();
        }
        else
        {
            logger.Error("SaveCommand: Save failed, hiding overlay. ScheduleId={ScheduleId}", scheduleId);
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
        }

        if (saved && isEnabled)
        {
            await toastService.ShowScheduledNotification(model);
        }
    }
}

