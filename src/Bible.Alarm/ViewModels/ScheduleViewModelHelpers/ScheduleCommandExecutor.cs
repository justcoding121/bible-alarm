#nullable enable
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Schedule;
using CommunityToolkit.Mvvm.Input;
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
    private readonly Action<bool>? setIsSaving;

    public ScheduleCommandExecutor(
        IScheduleCommandService scheduleCommandService,
        IScheduleMediaCacheService scheduleMediaCacheService,
        IState<ApplicationState> state,
        IState<PlaybackState> playbackState,
        IDispatcher dispatcher,
        IMapper mapper,
        ILogger logger,
        Func<MusicSelectionContainerViewModel?> getMusicSelectionContainerViewModel,
        Action<bool>? setIsSaving = null)
    {
        this.logger = logger;
        this.scheduleCommandService = scheduleCommandService;
        this.scheduleMediaCacheService = scheduleMediaCacheService;
        this.state = state;
        this.playbackState = playbackState;
        this.dispatcher = dispatcher;
        this.mapper = mapper;
        this.getMusicSelectionContainerViewModel = getMusicSelectionContainerViewModel;
        this.setIsSaving = setIsSaving;
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
        var currentSchedule = state.Value.CurrentSchedule;
        var isNewSchedule = GetScheduleId(currentSchedule) <= 0;
        await scheduleCommandService.ExecuteCancelAsync(isNewSchedule, GetScheduleId(currentSchedule), currentSchedule);
    }

    private async Task ExecuteSaveCommand()
    {
        logger.Information("SaveCommand: Save button clicked. IsNewSchedule={IsNewSchedule}, ScheduleId={ScheduleId}, Name={Name}",
            IsNewSchedule(), GetScheduleId(), GetName());

        // Set saving flag to prevent OnContentLoaded from hiding overlay
        setIsSaving?.Invoke(true);

        // Show busy overlay immediately when save is clicked
        // Dispatch state update first - this updates the state synchronously
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = true });
        logger.Debug("SaveCommand: Showing busy overlay immediately");

        // Ensure we're on the UI thread and wait for the state update to propagate to the UI
        // This gives the UI time to render the overlay before starting the save operation
        // The delay ensures:
        // 1. Fluxor state update completes
        // 2. ViewModel's OnStateChanged is called
        // 3. PropertyChanged event is raised
        // 4. Page's SyncOverlayWithState is called
        // 5. Overlay IsVisible is set to true
        // 6. UI renders the overlay
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            // Wait for state update to propagate and UI to render
            // Increased delay to 500ms to ensure overlay is visible before save actions trigger state changes
            await Task.Delay(500);
            logger.Debug("SaveCommand: Overlay should now be visible, starting save operation");
        });

        try
        {
            var currentSchedule = state.Value.CurrentSchedule;
            var isNewSchedule = IsNewSchedule();
            var scheduleId = GetScheduleId();

            var musicUpdated = DetectMusicChanges();
            var biblePublicationUpdated = DetectBiblePublicationChanges();

            await scheduleCommandService.StopPlaybackIfNeededAsync(
                isNewSchedule,
                playbackState.Value.IsPreparingOrPlaying,
                scheduleId,
                playbackState.Value.CurrentScheduleId ?? -1);

            if (currentSchedule != null)
            {
                var saved = await scheduleCommandService.ExecuteSaveAsync(
                    isNewSchedule,
                    scheduleId,
                    currentSchedule,
                    musicUpdated,
                    biblePublicationUpdated,
                    true); // modelInitialized

                if (saved)
                {
                    scheduleMediaCacheService.SetupMediaCache(scheduleId, isUpdate: !isNewSchedule);
                }

                var model = GetModel();
                await scheduleCommandService.HandleSaveResultAsync(saved, scheduleId, GetIsEnabled(), model);

                // On successful save, keep isSaving=true so overlay stays visible until page is destroyed by navigation
                // On failed save, HandleSaveResultAsync will handle hiding overlay, so clear the flag
                if (!saved)
                {
                    setIsSaving?.Invoke(false);
                }
            }
            else
            {
                // Hide overlay if currentSchedule is null
                logger.Warning("SaveCommand: CurrentSchedule is null, hiding overlay");
                dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
                setIsSaving?.Invoke(false);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error executing save command");
            // Hide overlay on error
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
            setIsSaving?.Invoke(false);
        }
    }

    private async Task ExecuteDeleteCommand()
    {
        logger.Information("DeleteCommand: Delete button clicked. IsNewSchedule={IsNewSchedule}, ScheduleId={ScheduleId}",
            IsNewSchedule(), GetScheduleId());

        // Set saving flag to prevent OnContentLoaded from hiding overlay
        setIsSaving?.Invoke(true);

        // Show busy overlay immediately when delete is clicked
        // Dispatch state update first - this updates the state synchronously
        dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = true });
        logger.Debug("DeleteCommand: Showing busy overlay immediately");

        // Ensure we're on the UI thread and wait for the state update to propagate to the UI
        // This gives the UI time to render the overlay before starting the delete operation
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            // Wait for state update to propagate and UI to render
            // 500ms should be enough for the state update -> ViewModel -> Page -> UI rendering chain
            await Task.Delay(500);
            logger.Debug("DeleteCommand: Overlay should now be visible, starting delete operation");
        });

        try
        {
            var isNewSchedule = IsNewSchedule();
            var scheduleId = GetScheduleId();

            await scheduleCommandService.StopPlaybackIfNeededAsync(
                isNewSchedule,
                playbackState.Value.IsPreparingOrPlaying,
                scheduleId,
                playbackState.Value.CurrentScheduleId ?? -1);

            // Only count saved schedules (Id > 0), not unsaved/new schedules
            var savedScheduleCount = state.Value.Schedules?.Count(s => s.Id > 0) ?? 0;
            var deleted = await scheduleCommandService.ExecuteDeleteAsync(isNewSchedule, scheduleId, savedScheduleCount);

            // On successful delete, keep isSaving=true so overlay stays visible until page is destroyed by navigation
            // On failed delete (validation failure), clear the flag
            if (!deleted)
            {
                setIsSaving?.Invoke(false);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error executing delete command");
            // Hide overlay on error
            dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });
            setIsSaving?.Invoke(false);
        }
    }

    // Helper methods for accessing state
    private int GetScheduleId() => state.Value.CurrentSchedule?.Id ?? -1;

    private int GetScheduleId(ScheduleStateItem? schedule) => schedule?.Id ?? -1;

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
        var hasMusicProperties = currentSchedule.MusicEnabled &&
                                 currentSchedule.MusicType.HasValue &&
                                 currentSchedule.MusicTrackNumber.HasValue &&
                                 currentSchedule.MusicTrackNumber.Value > 0;

        logger.Debug("DetectMusicChanges: musicContainer not found, hasMusicProperties={HasMusicProperties}", hasMusicProperties);
        return hasMusicProperties;
    }

    private bool DetectBiblePublicationChanges()
    {
        // This would need to track changes - simplified for now
        return false;
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
