#nullable enable

using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.Services;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

/// <summary>
/// Handles CreateScheduleAction effect logic.
/// </summary>
public class ScheduleCreateHandler
{
    private readonly IMapper mapper;
    private readonly IAlarmScheduleService? alarmScheduleService;
    private readonly IAlarmService? alarmService;
    private readonly ScheduleCacheManager cacheManager;

    public ScheduleCreateHandler(
        IMapper mapper,
        IAlarmScheduleService? alarmScheduleService,
        IAlarmService? alarmService,
        ScheduleCacheManager cacheManager)
    {
        this.mapper = mapper;
        this.alarmScheduleService = alarmScheduleService;
        this.alarmService = alarmService;
        this.cacheManager = cacheManager;
    }

    public async Task HandleAsync(CreateScheduleAction action, IDispatcher dispatcher)
    {
        try
        {
            Log.Information("ScheduleEffects: HandleCreateSchedule - Name: {Name}", action.Schedule?.Name);

            if (action.Schedule == null || alarmScheduleService == null)
            {
                Log.Warning("ScheduleEffects: HandleCreateSchedule - Schedule is null or service unavailable, skipping");
                if (action.Schedule != null)
                {
                    dispatcher.Dispatch(new CreateScheduleFailureAction(action.Schedule, "Service unavailable"));
                }
                return;
            }

            // Clear cache BEFORE save to prevent stale cache if process crashes
            cacheManager.InvalidateScheduleCache();

            // Map domain model (ScheduleStateItem) → DB entity (AlarmSchedule) on background thread
            var dbSchedule = await Task.Run(() => mapper.Map<AlarmSchedule>(action.Schedule));

            Log.Debug("ScheduleEffects: HandleCreateSchedule - Before save. PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                dbSchedule.BiblePublicationSchedule?.PublicationCode ?? "null",
                dbSchedule.BiblePublicationSchedule?.LanguageCode ?? "null");

            // Set ID to 0 for new schedule (EF Core will generate it)
            dbSchedule.Id = 0;

            // Save to database on background thread to avoid blocking UI
            var savedSchedule = await Task.Run(async () => 
                await alarmScheduleService.AddScheduleAsync(dbSchedule, CancellationToken.None));

            Log.Debug("ScheduleEffects: HandleCreateSchedule - After save. PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                savedSchedule.BiblePublicationSchedule?.PublicationCode ?? "null",
                savedSchedule.BiblePublicationSchedule?.LanguageCode ?? "null");

            Log.Information("ScheduleEffects: HandleCreateSchedule - Saved to DB. ScheduleId: {ScheduleId}", savedSchedule.Id);

            // Create alarm if enabled (on background thread)
            if (savedSchedule.IsEnabled && alarmService != null)
            {
                await Task.Run(() => alarmService.Create(savedSchedule));
            }

            // Map DB entity → domain model (ScheduleStateItem) on background thread
            var scheduleStateItem = await Task.Run(() =>
            {
                var mapped = mapper.Map<ScheduleStateItem>(savedSchedule);

                // IMPORTANT: Display names are already populated in action.Schedule (from CurrentSchedule state).
                // Selection pages/containers populate display names when user selects items (via HandleTrackSelected/HandleTrackSelected effects).
                // We should NOT query the database here - just preserve the display names from the action.
                // Copy display names from action.Schedule to scheduleStateItem (which was mapped from savedSchedule, so it doesn't have display names)
                if (action.Schedule != null)
                {
                    mapped.BiblePublicationLanguageName = action.Schedule.BiblePublicationLanguageName;
                    mapped.BiblePublicationPublicationName = action.Schedule.BiblePublicationPublicationName;
                    mapped.BiblePublicationSectionName = action.Schedule.BiblePublicationSectionName;
                    mapped.MusicLanguageName = action.Schedule.MusicLanguageName;
                    mapped.MusicPublicationName = action.Schedule.MusicPublicationName;
                    mapped.MusicTrackName = action.Schedule.MusicTrackName;
                }

                return mapped;
            });

            // Dispatch success action with DTO (display names preserved from state)
            dispatcher.Dispatch(new CreateScheduleSuccessAction(scheduleStateItem));

            Log.Information("ScheduleEffects: HandleCreateSchedule - Dispatched CreateScheduleSuccessAction for ScheduleId: {ScheduleId}",
                scheduleStateItem.Id);

            // Refresh cache in background after successful save
            _ = Task.Run(async () => await cacheManager.RefreshScheduleCacheAsync());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error in HandleCreateSchedule");
            if (action.Schedule != null)
            {
                dispatcher.Dispatch(new CreateScheduleFailureAction(action.Schedule, ex.Message));
            }
        }
    }
}

