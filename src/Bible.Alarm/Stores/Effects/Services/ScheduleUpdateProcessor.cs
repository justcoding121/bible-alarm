#nullable enable
using AutoMapper;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
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
    private readonly IMediaService? mediaService;

    public ScheduleUpdateProcessor(
        IMapper mapper,
        IAlarmScheduleService? alarmScheduleService = null,
        IAlarmService? alarmService = null,
        IScheduleDisplayNameService? scheduleDisplayNameService = null,
        IMediaService? mediaService = null)
    {
        this.mapper = mapper;
        this.alarmScheduleService = alarmScheduleService ?? ServiceProviderManager.GetService<IAlarmScheduleService>();
        this.alarmService = alarmService ?? ServiceProviderManager.GetService<IAlarmService>();
        this.scheduleDisplayNameService = scheduleDisplayNameService ?? ServiceProviderManager.GetService<IScheduleDisplayNameService>()!;
        this.mediaService = mediaService ?? ServiceProviderManager.GetService<IMediaService>();
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
            Log.Information("UpdateScheduleInDatabaseAsync: action.Schedule.NumberOfTracksToPlay={NumberOfTracksToPlay}, action.Schedule.AlwaysPlayFromStart={AlwaysPlayFromStart}",
                action.Schedule!.NumberOfTracksToPlay, action.Schedule.AlwaysPlayFromStart);
            
            var dbSchedule = mapper.Map<AlarmSchedule>(action.Schedule!);
            
            // Normalize LanguageCode for no-language publications (set to null instead of "E")
            await NormalizeLanguageCodeForNoLanguagePublicationsAsync(dbSchedule);
            
            Log.Information("UpdateScheduleInDatabaseAsync: After mapping - dbSchedule.NumberOfTracksToPlay={NumberOfTracksToPlay}, dbSchedule.AlwaysPlayFromStart={AlwaysPlayFromStart}",
                dbSchedule.NumberOfTracksToPlay, dbSchedule.AlwaysPlayFromStart);
            
            return await alarmScheduleService!.UpdateScheduleByIdAsync(
                action.Schedule.Id,
                existing => ScheduleEntityUpdater.UpdateScheduleEntity(existing, dbSchedule, action),
                CancellationToken.None);
        });

        LogScheduleUpdateResult(savedSchedule);
        Log.Information("UpdateScheduleInDatabaseAsync: After save - savedSchedule.NumberOfTracksToPlay={NumberOfTracksToPlay}, savedSchedule.AlwaysPlayFromStart={AlwaysPlayFromStart}",
            savedSchedule.NumberOfTracksToPlay, savedSchedule.AlwaysPlayFromStart);
        return savedSchedule;
    }

    /// <summary>
    /// Logs the result of a schedule update.
    /// </summary>
    public void LogScheduleUpdateResult(AlarmSchedule savedSchedule)
    {
        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updated in DB. ScheduleId: {ScheduleId}, savedSchedule.Music={HasMusic}, savedSchedule.Music.TrackCode={TrackCode}, savedSchedule.Music.PublicationCode={PublicationCode}, savedSchedule.Music.LanguageCode={LanguageCode}",
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

        return scheduleStateItem;
    }

    /// <summary>
    /// Logs the mapping result.
    /// </summary>
    public void LogMappingResult(ScheduleStateItem scheduleStateItem)
    {
        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - After mapping savedSchedule to scheduleStateItem. scheduleStateItem.MusicPublicationCode={PublicationCode}, scheduleStateItem.MusicLanguageCode={LanguageCode}, scheduleStateItem.MusicTrackCode={TrackCode}",
            scheduleStateItem.MusicPublicationCode ?? "null",
            scheduleStateItem.MusicLanguageCode ?? "null",
            scheduleStateItem.MusicTrackCode?.ToString() ?? "null");
    }

    /// <summary>
    /// Copies display names from action schedule to state item.
    /// </summary>
    public void CopyDisplayNamesFromAction(ScheduleStateItem scheduleStateItem, ScheduleStateItem actionSchedule)
    {
        // Deprecated: Schedule display names are hydrated from media index on save.
        // Kept only to avoid breaking older call sites; do not use.
    }

    /// <summary>
    /// Preserves music properties if needed.
    /// Music type is inferred from LanguageCode: null/empty = melody (instrumental), otherwise = vocal.
    /// </summary>
    public void PreserveMusicPropertiesIfNeeded(ScheduleStateItem scheduleStateItem, ScheduleStateItem actionSchedule)
    {
        // Check if publication code mismatches - if so, preserve from action
        if (!string.IsNullOrEmpty(actionSchedule.MusicPublicationCode) &&
            scheduleStateItem.MusicPublicationCode != actionSchedule.MusicPublicationCode)
        {
            Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - Music publication mismatch! savedSchedule.Music.PublicationCode={SavedPublicationCode}, action.Schedule.MusicPublicationCode={ActionPublicationCode}. Using action.Schedule properties.",
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

    /// <summary>
    /// Normalizes LanguageCode for no-language publications by setting it to null.
    /// "E" is used in state/UI as a fallback but should not be persisted to DB.
    /// </summary>
    private async Task NormalizeLanguageCodeForNoLanguagePublicationsAsync(AlarmSchedule model)
    {
        if (mediaService == null)
        {
            return;
        }

        // Check Bible publication
        if (model.BiblePublicationSchedule != null && !string.IsNullOrEmpty(model.BiblePublicationSchedule.PublicationCode))
        {
            var isNoLanguage = await mediaService.IsPublicationWithoutLanguageAsync(model.BiblePublicationSchedule.PublicationCode);
            if (isNoLanguage && model.BiblePublicationSchedule.LanguageCode == "E")
            {
                Log.Information("NormalizeLanguageCodeForNoLanguagePublicationsAsync: Setting BiblePublicationSchedule.LanguageCode to null for no-language publication {PublicationCode}",
                    model.BiblePublicationSchedule.PublicationCode);
                model.BiblePublicationSchedule.LanguageCode = null;
            }
        }

        // Check Music publication (instrumental music is no-language)
        if (model.Music != null && !string.IsNullOrEmpty(model.Music.PublicationCode))
        {
            var isNoLanguage = await mediaService.IsPublicationWithoutLanguageAsync(model.Music.PublicationCode);
            if (isNoLanguage && model.Music.LanguageCode == "E")
            {
                Log.Information("NormalizeLanguageCodeForNoLanguagePublicationsAsync: Setting Music.LanguageCode to null for no-language publication {PublicationCode}",
                    model.Music.PublicationCode);
                model.Music.LanguageCode = null;
            }
        }
    }
}

