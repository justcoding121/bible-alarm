#nullable enable
using AutoMapper;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;
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
    private readonly IScheduleDisplayNameService scheduleDisplayNameService;

    public ScheduleUpdateProcessor(
        IMapper mapper,
        IAlarmScheduleService? alarmScheduleService = null,
        IAlarmService? alarmService = null,
        IScheduleDisplayNameService? scheduleDisplayNameService = null)
    {
        this.mapper = mapper;
        this.alarmScheduleService = alarmScheduleService ?? ServiceProviderManager.GetService<IAlarmScheduleService>();
        this.alarmService = alarmService ?? ServiceProviderManager.GetService<IAlarmService>();
        this.scheduleDisplayNameService = scheduleDisplayNameService ?? ServiceProviderManager.GetService<IScheduleDisplayNameService>()!;
    }

    /// <summary>
    /// Logs the start of an update operation.
    /// </summary>
    public static void LogUpdateStart(UpdateScheduleFromViewModelAction action)
    {
        Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelScheduleIdNameShouldSave,
            action.Schedule?.Id, action.Schedule?.Name, action.ShouldSave);
    }

    /// <summary>
    /// Handles service unavailability.
    /// </summary>
    public static void HandleServiceUnavailable(UpdateScheduleFromViewModelAction action, IDispatcher dispatcher)
    {
        Log.Warning(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelServiceUnavailableSkipping);
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
            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.UpdateScheduleInDatabaseAsyncActionScheduleTracksAndAlwaysPlay,
                action.Schedule!.NumberOfTracksToPlay, action.Schedule.AlwaysPlayFromStart);
            
            var dbSchedule = mapper.Map<AlarmSchedule>(action.Schedule!);

            // Keep the selected language code (e.g. E, MY) for no-language publications so the UI
            // shows the correct language name when the schedule is viewed again.
            
            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.UpdateScheduleInDatabaseAsyncAfterMappingTracksAndAlwaysPlay,
                dbSchedule.NumberOfTracksToPlay, dbSchedule.AlwaysPlayFromStart);
            
            return await alarmScheduleService!.UpdateScheduleByIdAsync(
                action.Schedule.Id,
                existing => ScheduleEntityUpdater.UpdateScheduleEntity(existing, dbSchedule, action),
                CancellationToken.None);
        });

        LogScheduleUpdateResult(savedSchedule);
        Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.UpdateScheduleInDatabaseAsyncAfterSaveTracksAndAlwaysPlay,
            savedSchedule.NumberOfTracksToPlay, savedSchedule.AlwaysPlayFromStart);
        return savedSchedule;
    }

    /// <summary>
    /// Logs the result of a schedule update.
    /// </summary>
    public static void LogScheduleUpdateResult(AlarmSchedule savedSchedule)
    {
        Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelUpdatedInDbMusicFields,
            savedSchedule.Id,
            savedSchedule.Music != null ? "not null" : "null",
            savedSchedule.Music?.TrackCode.ToString() ?? "null",
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
        var scheduleStateItem = mapper.Map<ScheduleStateItem>(savedSchedule);
        LogMappingResult(scheduleStateItem);

        if (action.Schedule != null)
        {
            // Preserve key music selection fields if the DB mapping lags behind in edge cases.
            PreserveMusicPropertiesIfNeeded(scheduleStateItem, action.Schedule);
        }

        // Populate display names from media index (single-schedule hydration).
        await scheduleDisplayNameService.PopulateDisplayNamesAsync(scheduleStateItem, savedSchedule);

        // After save: preserve music language display names from action so home list shows e.g. Malayalam not English for melody (PopulateDisplayNamesAsync may not resolve MY in time; action has the UI value).
        if (action.Schedule != null && !string.IsNullOrWhiteSpace(scheduleStateItem.MusicPublicationCode) &&
            scheduleStateItem.MusicPublicationCode == action.Schedule.MusicPublicationCode &&
            scheduleStateItem.MusicLanguageCode == action.Schedule.MusicLanguageCode)
        {
            if (!string.IsNullOrWhiteSpace(action.Schedule.MusicLanguageName))
                scheduleStateItem.MusicLanguageName = action.Schedule.MusicLanguageName;
            if (!string.IsNullOrWhiteSpace(action.Schedule.MusicLanguageDirection))
                scheduleStateItem.MusicLanguageDirection = action.Schedule.MusicLanguageDirection;
        }

        return scheduleStateItem;
    }

    /// <summary>
    /// Logs the mapping result.
    /// </summary>
    public static void LogMappingResult(ScheduleStateItem scheduleStateItem)
    {
        Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelAfterMappingScheduleStateItemMusicFields,
            scheduleStateItem.MusicPublicationCode ?? "null",
            scheduleStateItem.MusicLanguageCode ?? "null",
            scheduleStateItem.MusicTrackCode?.ToString() ?? "null");
    }

    /// <summary>
    /// Preserves music properties if needed.
    /// Music type is inferred from LanguageCode: null/empty = melody (instrumental), otherwise = vocal.
    /// </summary>
    public static void PreserveMusicPropertiesIfNeeded(ScheduleStateItem scheduleStateItem, ScheduleStateItem actionSchedule)
    {
        // Check if publication code mismatches - if so, preserve from action
        if (!string.IsNullOrEmpty(actionSchedule.MusicPublicationCode) &&
            scheduleStateItem.MusicPublicationCode != actionSchedule.MusicPublicationCode)
        {
            Log.Warning(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelMusicPublicationMismatch,
                scheduleStateItem.MusicPublicationCode ?? "null",
                actionSchedule.MusicPublicationCode ?? "null");

            scheduleStateItem.MusicTrackCode = actionSchedule.MusicTrackCode;
            scheduleStateItem.MusicPublicationCode = actionSchedule.MusicPublicationCode;
            scheduleStateItem.MusicLanguageCode = actionSchedule.MusicLanguageCode;
            scheduleStateItem.MusicRepeat = actionSchedule.MusicRepeat;
            scheduleStateItem.MusicId = actionSchedule.MusicId;
            scheduleStateItem.MusicSectionCode = actionSchedule.MusicSectionCode;
        }
    }

}

