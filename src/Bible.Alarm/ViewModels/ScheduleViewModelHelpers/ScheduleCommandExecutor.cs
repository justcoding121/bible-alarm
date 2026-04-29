#nullable enable
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Schedule;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

/// <summary>
/// Handles command execution for ScheduleViewModel.
/// </summary>
public sealed class ScheduleCommandExecutor
{
    private readonly ILogger logger;
    private readonly IScheduleCommandService scheduleCommandService;
    private readonly IScheduleMediaCacheService scheduleMediaCacheService;
    private readonly IState<ApplicationState> state;
    private readonly IState<PlaybackState> playbackState;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;
    private readonly Func<MusicSelectionContainerViewModel?> getMusicSelectionContainerViewModel;
    private readonly Func<AlarmSettingsContainerViewModel?>? getAlarmSettingsContainerViewModel;
    private readonly Func<NumberOfTrackContainerViewModel?>? getNumberOfTrackContainerViewModel;
    private readonly Action<bool>? setIsSaving;
    private readonly Action<bool>? setIsCancelBusy;
    private readonly Action<bool>? setIsSaveBusy;
    private readonly Action<bool>? setIsDeleteBusy;

    public ScheduleCommandExecutor(
        IScheduleCommandService scheduleCommandService,
        IScheduleMediaCacheService scheduleMediaCacheService,
        IState<ApplicationState> state,
        IState<PlaybackState> playbackState,
        IDispatcher dispatcher,
        IMapper mapper,
        ILogger logger,
        Func<MusicSelectionContainerViewModel?> getMusicSelectionContainerViewModel,
        Func<AlarmSettingsContainerViewModel?>? getAlarmSettingsContainerViewModel = null,
        Func<NumberOfTrackContainerViewModel?>? getNumberOfTrackContainerViewModel = null,
        Action<bool>? setIsSaving = null,
        Action<bool>? setIsCancelBusy = null,
        Action<bool>? setIsSaveBusy = null,
        Action<bool>? setIsDeleteBusy = null)
    {
        this.logger = logger;
        this.scheduleCommandService = scheduleCommandService;
        this.scheduleMediaCacheService = scheduleMediaCacheService;
        this.state = state;
        this.playbackState = playbackState;
        this.dispatcher = dispatcher;
        this.mapper = mapper;
        this.getMusicSelectionContainerViewModel = getMusicSelectionContainerViewModel;
        this.getAlarmSettingsContainerViewModel = getAlarmSettingsContainerViewModel;
        this.getNumberOfTrackContainerViewModel = getNumberOfTrackContainerViewModel;
        this.setIsSaving = setIsSaving;
        this.setIsCancelBusy = setIsCancelBusy;
        this.setIsSaveBusy = setIsSaveBusy;
        this.setIsDeleteBusy = setIsDeleteBusy;
    }

    public void InitializeCommands(
        out ICommand cancelCommand,
        out ICommand saveCommand,
        out ICommand deleteCommand)
    {
        cancelCommand = new AsyncRelayCommand(ExecuteCancelCommand);
        saveCommand = new AsyncRelayCommand(ExecuteSaveCommand);
        deleteCommand = new AsyncRelayCommand(ExecuteDeleteCommand);
    }

    private async Task ExecuteCancelCommand()
    {
        // Set IsCancelBusy immediately to show loading indicator
        setIsCancelBusy?.Invoke(true);
        
        // Wait 50ms to ensure UI thread renders the update before doing backend work
        await Task.Delay(50);

        try
        {
            var currentSchedule = state.Value.CurrentSchedule;
            var isNewSchedule = GetScheduleId(currentSchedule) <= 0;
            await scheduleCommandService.ExecuteCancelAsync(isNewSchedule, GetScheduleId(currentSchedule), currentSchedule);
        }
        finally
        {
            // Reset IsCancelBusy after operation completes
            setIsCancelBusy?.Invoke(false);
        }
    }

    private async Task ExecuteSaveCommand()
    {
        logger.Information("SaveCommand: Save button clicked. IsNewSchedule={IsNewSchedule}, ScheduleId={ScheduleId}, Name={Name}",
            IsNewSchedule(), GetScheduleId(), GetName());

        // Set IsSaveBusy immediately to show loading indicator
        setIsSaveBusy?.Invoke(true);
        setIsSaving?.Invoke(true);
        
        // Wait 50ms to ensure UI thread renders the update before doing backend work
        await Task.Delay(50);

        // Show busy overlay after progress indicator is visible
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = true });
        logger.Debug("SaveCommand: Showing busy overlay");

        // Yield so the queued BeginInvokeOnMainThread property update executes
        // and the XAML overlay actually renders before the save proceeds
        await Task.Delay(50);

        try
        {
            // Read current schedule fresh from state to ensure we have the latest values
            // (including NumberOfTracksToPlay and AlwaysPlayFromStart that may have been updated)
            var currentSchedule = state.Value.CurrentSchedule;
            var isNewSchedule = IsNewSchedule();
            var scheduleId = GetScheduleId();

            // Sync NotificationEnabled from ViewModel property to ensure we have the latest value
            // This is important because permission polling may have updated the ViewModel property
            // but the state update might not have been processed yet
#if ANDROID
            var alarmSettingsViewModel = getAlarmSettingsContainerViewModel?.Invoke();
            var numberOfTrackViewModel = getNumberOfTrackContainerViewModel?.Invoke();
            
            if (currentSchedule != null)
            {
                // Check AlarmSettingsContainerViewModel first (primary source for NotificationEnabled)
                if (alarmSettingsViewModel != null && currentSchedule.NotificationEnabled != alarmSettingsViewModel.NotificationEnabled)
                {
                    logger.Information("SaveCommand: Syncing NotificationEnabled from AlarmSettingsContainerViewModel. State={StateValue}, ViewModel={ViewModelValue}",
                        currentSchedule.NotificationEnabled, alarmSettingsViewModel.NotificationEnabled);
                    
                    currentSchedule = mapper.Map<ScheduleStateItem>(currentSchedule);
                    currentSchedule.NotificationEnabled = alarmSettingsViewModel.NotificationEnabled;
                    
                    // Dispatch state update to prevent OnStateChanged from overwriting ViewModel
                    // Use shouldSave: false to avoid triggering another save cycle
                    dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(currentSchedule, false, false, shouldSave: false));
                    
                    // Wait briefly for state update to process
                    await Task.Delay(50);
                    
                    // Re-read from state to get the updated value
                    currentSchedule = state.Value.CurrentSchedule;
                }
                // Fallback to NumberOfTrackContainerViewModel if AlarmSettingsContainerViewModel is not available
                else if (numberOfTrackViewModel != null && currentSchedule.NotificationEnabled != numberOfTrackViewModel.NotificationEnabled)
                {
                    logger.Information("SaveCommand: Syncing NotificationEnabled from NumberOfTrackContainerViewModel. State={StateValue}, ViewModel={ViewModelValue}",
                        currentSchedule.NotificationEnabled, numberOfTrackViewModel.NotificationEnabled);
                    
                    currentSchedule = mapper.Map<ScheduleStateItem>(currentSchedule);
                    currentSchedule.NotificationEnabled = numberOfTrackViewModel.NotificationEnabled;
                    
                    // Dispatch state update to prevent OnStateChanged from overwriting ViewModel
                    // Use shouldSave: false to avoid triggering another save cycle
                    dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(currentSchedule, false, false, shouldSave: false));
                    
                    // Wait briefly for state update to process
                    await Task.Delay(50);
                    
                    // Re-read from state to get the updated value
                    currentSchedule = state.Value.CurrentSchedule;
                }
            }
#endif

            logger.Information("SaveCommand: Reading state - currentSchedule.NumberOfTracksToPlay={NumberOfTracksToPlay}, currentSchedule.AlwaysPlayFromStart={AlwaysPlayFromStart}, currentSchedule.NotificationEnabled={NotificationEnabled}",
                currentSchedule?.NumberOfTracksToPlay ?? 0, currentSchedule?.AlwaysPlayFromStart ?? false, currentSchedule?.NotificationEnabled ?? false);

            var musicUpdated = DetectMusicChanges();
            var biblePublicationUpdated = DetectBiblePublicationChanges();

            if (biblePublicationUpdated)
            {
                await scheduleCommandService.StopPlaybackIfNeededAsync(
                    isNewSchedule,
                    playbackState.Value.IsPreparingOrPlaying,
                    scheduleId,
                    playbackState.Value.CurrentScheduleId ?? -1);
            }

            if (currentSchedule != null)
            {
                var saved = await scheduleCommandService.ExecuteSaveAsync(
                    isNewSchedule,
                    scheduleId,
                    currentSchedule,
                    musicUpdated,
                    biblePublicationUpdated,
                    // modelInitialized
                    true);

                if (saved)
                {
                    // Setup media cache - DeleteScheduleCacheAsync will automatically
                    // compute the new schedule's URLs and delete only files that don't match
                    scheduleMediaCacheService.SetupMediaCache(scheduleId, isUpdate: !isNewSchedule);
                }

                var model = GetModel();
                await scheduleCommandService.HandleSaveResultAsync(saved, scheduleId, GetIsEnabled(), model);

                // On successful save, keep isSaving=true so overlay stays visible until page is destroyed by navigation
                // On failed save, HandleSaveResultAsync will handle hiding overlay, so clear the flag
                if (!saved)
                {
                    setIsSaving?.Invoke(false);
                    setIsSaveBusy?.Invoke(false);
                }
            }
            else
            {
                // Hide overlay if currentSchedule is null
                logger.Warning("SaveCommand: CurrentSchedule is null, hiding overlay");
                dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
                setIsSaving?.Invoke(false);
                setIsSaveBusy?.Invoke(false);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error executing save command");
            // Hide overlay on error
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
            setIsSaving?.Invoke(false);
            setIsSaveBusy?.Invoke(false);
        }
    }

    private async Task ExecuteDeleteCommand()
    {
        logger.Information("DeleteCommand: Delete button clicked. IsNewSchedule={IsNewSchedule}, ScheduleId={ScheduleId}",
            IsNewSchedule(), GetScheduleId());

        var isNewSchedule = IsNewSchedule();
        var scheduleId = GetScheduleId();

        // Check if this is the last schedule BEFORE showing overlay or doing any work
        // Only count saved schedules (Id > 0), not unsaved/new schedules
        var savedScheduleCount = state.Value.Schedules?.Count(s => s.Id > 0) ?? 0;
        
        if (!isNewSchedule && scheduleId > 0 && savedScheduleCount <= 1)
        {
            logger.Warning("DeleteCommand: Cannot delete schedule {ScheduleId} - it is the last schedule", scheduleId);
            WeakReferenceMessenger.Default.Send(new ShowToastMessage("Cannot delete last schedule"));
            return;
        }

        // Set IsDeleteBusy immediately to show loading indicator
        setIsDeleteBusy?.Invoke(true);
        setIsSaving?.Invoke(true);
        
        // Wait 50ms to ensure UI thread renders the update before doing backend work
        await Task.Delay(50);

        // Show busy overlay after progress indicator is visible
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = true });
        logger.Debug("DeleteCommand: Showing busy overlay");

        // Yield so the queued BeginInvokeOnMainThread property update executes
        // and the XAML overlay actually renders before the delete proceeds
        await Task.Delay(50);

        try
        {
            await scheduleCommandService.StopPlaybackIfNeededAsync(
                isNewSchedule,
                playbackState.Value.IsPreparingOrPlaying,
                scheduleId,
                playbackState.Value.CurrentScheduleId ?? -1);

            var deleted = await scheduleCommandService.ExecuteDeleteAsync(isNewSchedule, scheduleId, savedScheduleCount);

            // On successful delete, keep isSaving=true so overlay stays visible until page is destroyed by navigation
            // On failed delete (validation failure), clear the flag
            if (!deleted)
            {
                setIsSaving?.Invoke(false);
                setIsDeleteBusy?.Invoke(false);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error executing delete command");
            // Hide overlay on error
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
            setIsSaving?.Invoke(false);
            setIsDeleteBusy?.Invoke(false);
        }
    }

    // Helper methods for accessing state
    private int GetScheduleId() => state.Value.CurrentSchedule?.Id ?? -1;

    private static int GetScheduleId(ScheduleStateItem? schedule) => schedule?.Id ?? -1;

    private bool IsNewSchedule() => GetScheduleId() <= 0;

    private string GetName() => state.Value.CurrentSchedule?.Name ?? string.Empty;

    private bool GetIsEnabled() => state.Value.CurrentSchedule?.IsEnabled ?? false;

    private bool DetectMusicChanges()
    {
        // Check if music was updated via MusicSelectionContainerViewModel
        var musicContainer = getMusicSelectionContainerViewModel();
        if (musicContainer != null)
        {
            var musicUpdated = musicContainer.GetMusicUpdated();
            logger.Debug("DetectMusicChanges: musicContainer found, MusicUpdated={MusicUpdated}", musicUpdated);
            return musicUpdated;
        }

        // Fallback: check if CurrentSchedule has music properties that differ from what's saved
        // This handles cases where the container isn't available yet
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return false;
        }

        // If music is enabled and has properties, assume it might have changed
        // This is a conservative check - we'll let the save logic handle the actual comparison
        // Music type is inferred from LanguageCode: NULL/empty = instrumental, otherwise = vocal
        var hasMusicProperties = currentSchedule.MusicEnabled &&
                                 !string.IsNullOrEmpty(currentSchedule.MusicPublicationCode) &&
                                 !string.IsNullOrWhiteSpace(currentSchedule.MusicTrackCode);

        logger.Debug("DetectMusicChanges: musicContainer not found, hasMusicProperties={HasMusicProperties}", hasMusicProperties);
        return hasMusicProperties;
    }

    private bool DetectBiblePublicationChanges()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return false;
        }

        // New schedules have no persisted progress to reset.
        if (currentSchedule.Id <= 0)
        {
            return false;
        }

        // Compare against the last-saved (persisted) schedule in the Schedules list.
        // The schedule list is only updated on save, so this acts as the "track when opened" baseline
        // for normal edit sessions.
        var persisted = state.Value.Schedules?.FirstOrDefault(s => s.Id == currentSchedule.Id);
        if (persisted == null)
        {
            return false;
        }

        static string? Norm(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        var sameLanguage = string.Equals(Norm(persisted.BiblePublicationLanguageCode), Norm(currentSchedule.BiblePublicationLanguageCode), StringComparison.OrdinalIgnoreCase);
        var samePublication = string.Equals(Norm(persisted.BiblePublicationCode), Norm(currentSchedule.BiblePublicationCode), StringComparison.OrdinalIgnoreCase);
        var sameSection = string.Equals(SectionCodeHelper.Normalize(persisted.BiblePublicationSectionCode), SectionCodeHelper.Normalize(currentSchedule.BiblePublicationSectionCode), StringComparison.OrdinalIgnoreCase);
        var sameTrack = (persisted.BiblePublicationTrackCode ?? string.Empty) == (currentSchedule.BiblePublicationTrackCode ?? string.Empty);

        return !(sameLanguage && samePublication && sameSection && sameTrack);
    }

    private AlarmSchedule GetModel()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return new AlarmSchedule { Id = 0 };
        }
        return mapper.Map<AlarmSchedule>(currentSchedule);
    }
}
