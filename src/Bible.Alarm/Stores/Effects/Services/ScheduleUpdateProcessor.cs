#nullable enable
using AutoMapper;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Stores.Effects.Services;

/// <summary>
/// Handles processing of schedule update operations.
/// Separated from ScheduleEffects for better modularity.
/// </summary>
public sealed class ScheduleUpdateProcessor
{
    private readonly IMapper mapper;
    private readonly IAlarmScheduleService? alarmScheduleService;
    private readonly IAlarmService? alarmService;

    public ScheduleUpdateProcessor(
        IMapper mapper,
        IAlarmScheduleService? alarmScheduleService = null,
        IAlarmService? alarmService = null)
    {
        this.mapper = mapper;
        this.alarmScheduleService = alarmScheduleService ?? ServiceProviderManager.GetService<IAlarmScheduleService>();
        this.alarmService = alarmService ?? ServiceProviderManager.GetService<IAlarmService>();
    }

    /// <summary>
    /// Logs the start of an update operation.
    /// </summary>
    public void LogUpdateStart(UpdateScheduleFromViewModelAction action)
    {
        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - ScheduleId: {ScheduleId}, Name: {Name}, ShouldSave: {ShouldSave}",
            action.Schedule?.Id, action.Schedule?.Name, action.ShouldSave);
    }

    /// <summary>
    /// Handles service unavailability.
    /// </summary>
    public void HandleServiceUnavailable(UpdateScheduleFromViewModelAction action, IDispatcher dispatcher)
    {
        Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - Service unavailable, skipping");
        dispatcher.Dispatch(new UpdateScheduleFailureAction(action.Schedule!, "Service unavailable"));
    }

    /// <summary>
    /// Updates schedule in database.
    /// </summary>
    public async Task<AlarmSchedule> UpdateScheduleInDatabaseAsync(UpdateScheduleFromViewModelAction action)
    {
        // Map and run database update on background thread to avoid blocking UI
        var savedSchedule = await Task.Run(async () =>
        {
            var dbSchedule = mapper.Map<AlarmSchedule>(action.Schedule!);
            return await alarmScheduleService!.UpdateScheduleByIdAsync(
                action.Schedule.Id,
                existing => ScheduleEntityUpdater.UpdateScheduleEntity(existing, dbSchedule, action),
                CancellationToken.None);
        });

        LogScheduleUpdateResult(savedSchedule);
        return savedSchedule;
    }

    /// <summary>
    /// Logs the result of a schedule update.
    /// </summary>
    public void LogScheduleUpdateResult(AlarmSchedule savedSchedule)
    {
        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updated in DB. ScheduleId: {ScheduleId}, savedSchedule.Music={HasMusic}, savedSchedule.Music.MusicType={MusicType}, savedSchedule.Music.TrackNumber={TrackNumber}, savedSchedule.Music.PublicationCode={PublicationCode}, savedSchedule.Music.LanguageCode={LanguageCode}",
            savedSchedule.Id,
            savedSchedule.Music != null ? "not null" : "null",
            savedSchedule.Music?.MusicType.ToString() ?? "null",
            savedSchedule.Music?.TrackNumber.ToString() ?? "null",
            savedSchedule.Music?.PublicationCode ?? "null",
            savedSchedule.Music?.LanguageCode ?? "null");
    }

    /// <summary>
    /// Updates alarm for the schedule.
    /// </summary>
    public async Task UpdateAlarmAsync(AlarmSchedule savedSchedule)
    {
        if (alarmService != null)
        {
            await alarmService.Update(savedSchedule);
        }
    }

    /// <summary>
    /// Maps saved schedule to state item and preserves display names from action.
    /// Runs on background thread to avoid blocking UI.
    /// </summary>
    public async Task<ScheduleStateItem> MapAndPreserveDisplayNames(UpdateScheduleFromViewModelAction action, AlarmSchedule savedSchedule)
    {
        return await Task.Run(() =>
        {
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(savedSchedule);
            LogMappingResult(scheduleStateItem);

            if (action.Schedule != null)
            {
                CopyDisplayNamesFromAction(scheduleStateItem, action.Schedule);
                PreserveMusicPropertiesIfNeeded(scheduleStateItem, action.Schedule);
            }

            return scheduleStateItem;
        });
    }

    /// <summary>
    /// Logs the mapping result.
    /// </summary>
    public void LogMappingResult(ScheduleStateItem scheduleStateItem)
    {
        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - After mapping savedSchedule to scheduleStateItem. scheduleStateItem.MusicType={MusicType}, scheduleStateItem.MusicTrackNumber={TrackNumber}, scheduleStateItem.MusicPublicationCode={PublicationCode}, scheduleStateItem.MusicLanguageCode={LanguageCode}",
            scheduleStateItem.MusicType?.ToString() ?? "null",
            scheduleStateItem.MusicTrackNumber?.ToString() ?? "null",
            scheduleStateItem.MusicPublicationCode ?? "null",
            scheduleStateItem.MusicLanguageCode ?? "null");
    }

    /// <summary>
    /// Copies display names from action schedule to state item.
    /// </summary>
    public void CopyDisplayNamesFromAction(ScheduleStateItem scheduleStateItem, ScheduleStateItem actionSchedule)
    {
        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Copying display names from action.Schedule. action.Schedule.MusicType={MusicType}, action.Schedule.MusicTrackNumber={TrackNumber}",
            actionSchedule.MusicType?.ToString() ?? "null",
            actionSchedule.MusicTrackNumber?.ToString() ?? "null");

        scheduleStateItem.BiblePublicationLanguageName = actionSchedule.BiblePublicationLanguageName;
        scheduleStateItem.BiblePublicationName = actionSchedule.BiblePublicationName;
        scheduleStateItem.BiblePublicationSectionName = actionSchedule.BiblePublicationSectionName;
        scheduleStateItem.MusicLanguageName = actionSchedule.MusicLanguageName;
        scheduleStateItem.MusicPublicationName = actionSchedule.MusicPublicationName;
        scheduleStateItem.MusicTrackName = actionSchedule.MusicTrackName;
    }

    /// <summary>
    /// Preserves music properties if needed.
    /// </summary>
    public void PreserveMusicPropertiesIfNeeded(ScheduleStateItem scheduleStateItem, ScheduleStateItem actionSchedule)
    {
        if (actionSchedule.MusicType.HasValue &&
            (!scheduleStateItem.MusicType.HasValue || scheduleStateItem.MusicType.Value != actionSchedule.MusicType.Value))
        {
            Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - MusicType mismatch! savedSchedule.Music.MusicType={SavedMusicType}, action.Schedule.MusicType={ActionMusicType}. Using action.Schedule.MusicType.",
                scheduleStateItem.MusicType?.ToString() ?? "null",
                actionSchedule.MusicType?.ToString() ?? "null");

            scheduleStateItem.MusicType = actionSchedule.MusicType;
            scheduleStateItem.MusicTrackNumber = actionSchedule.MusicTrackNumber;
            scheduleStateItem.MusicPublicationCode = actionSchedule.MusicPublicationCode;
            scheduleStateItem.MusicLanguageCode = actionSchedule.MusicLanguageCode;
            scheduleStateItem.MusicRepeat = actionSchedule.MusicRepeat;
            scheduleStateItem.MusicId = actionSchedule.MusicId;
        }
    }
}

