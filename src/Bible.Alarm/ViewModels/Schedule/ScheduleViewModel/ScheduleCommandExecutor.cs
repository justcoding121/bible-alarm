#nullable enable
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule.ScheduleViewModel;

/// <summary>
/// Handles command execution for ScheduleViewModel.
/// </summary>
public sealed class ScheduleCommandExecutor(ILogger logger)
{
    private readonly IScheduleCommandService scheduleCommandService;
    private readonly IScheduleMediaCacheService scheduleMediaCacheService;
    private readonly IState<ApplicationState> state;
    private readonly IState<PlaybackState> playbackState;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;

    public ScheduleCommandExecutor(
        IScheduleCommandService scheduleCommandService,
        IScheduleMediaCacheService scheduleMediaCacheService,
        IState<ApplicationState> state,
        IState<PlaybackState> playbackState,
        IDispatcher dispatcher,
        IMapper mapper)
    {
        this.scheduleCommandService = scheduleCommandService;
        this.scheduleMediaCacheService = scheduleMediaCacheService;
        this.state = state;
        this.playbackState = playbackState;
        this.dispatcher = dispatcher;
        this.mapper = mapper;
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

        try
        {
            var currentSchedule = state.Value.CurrentSchedule;
            var isNewSchedule = IsNewSchedule();
            var scheduleId = GetScheduleId();

            var musicUpdated = DetectMusicChanges();
            var bibleReadingUpdated = DetectBibleReadingChanges();

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
                    bibleReadingUpdated,
                    true); // modelInitialized

                if (saved)
                {
                    scheduleMediaCacheService.SetupMediaCache(scheduleId, isUpdate: !isNewSchedule);
                }

                var model = GetModel();
                await scheduleCommandService.HandleSaveResultAsync(saved, scheduleId, GetIsEnabled(), model);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error executing save command");
        }
    }

    private async Task ExecuteDeleteCommand()
    {
        var isNewSchedule = IsNewSchedule();
        var scheduleId = GetScheduleId();

        await scheduleCommandService.StopPlaybackIfNeededAsync(
            isNewSchedule,
            playbackState.Value.IsPreparingOrPlaying,
            scheduleId,
            playbackState.Value.CurrentScheduleId ?? -1);

        var scheduleCount = state.Value.Schedules?.Count ?? 0;
        await scheduleCommandService.ExecuteDeleteAsync(isNewSchedule, scheduleId, scheduleCount);
    }

    // Helper methods for accessing state
    private int GetScheduleId() => state.Value.CurrentSchedule?.Id ?? -1;

    private int GetScheduleId(ScheduleStateItem? schedule) => schedule?.Id ?? -1;

    private bool IsNewSchedule() => GetScheduleId() <= 0;

    private string GetName() => state.Value.CurrentSchedule?.Name ?? string.Empty;

    private bool GetIsEnabled() => state.Value.CurrentSchedule?.IsEnabled ?? false;

    private bool DetectMusicChanges()
    {
        // This would need to track changes - simplified for now
        return false;
    }

    private bool DetectBibleReadingChanges()
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
