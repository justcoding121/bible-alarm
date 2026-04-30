#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
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
    private readonly IScheduleDisplayNameService scheduleDisplayNameService;

    public ScheduleCreateHandler(
        IMapper mapper,
        IAlarmScheduleService? alarmScheduleService,
        IAlarmService? alarmService,
        IScheduleDisplayNameService scheduleDisplayNameService)
    {
        this.mapper = mapper;
        this.alarmScheduleService = alarmScheduleService;
        this.alarmService = alarmService;
        this.scheduleDisplayNameService = scheduleDisplayNameService;
    }

    public async Task HandleAsync(CreateScheduleAction action, IDispatcher dispatcher)
    {
        try
        {
            Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleCreateScheduleName, action.Schedule?.Name);

            if (action.Schedule == null || alarmScheduleService == null)
            {
                Log.Warning(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleCreateScheduleScheduleNullOrServiceUnavailable);
                if (action.Schedule != null)
                {
                    dispatcher.Dispatch(new CreateScheduleFailureAction(action.Schedule, "Service unavailable"));
                }
                return;
            }

            // Map domain model (ScheduleStateItem) → DB entity (AlarmSchedule) on background thread
            var dbSchedule = await Task.Run(() => mapper.Map<AlarmSchedule>(action.Schedule));

            Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleCreateScheduleBeforeSavePublicationLanguage,
                dbSchedule.BiblePublicationSchedule?.PublicationCode ?? "null",
                dbSchedule.BiblePublicationSchedule?.LanguageCode ?? "null");

            // Set ID to 0 for new schedule (EF Core will generate it)
            dbSchedule.Id = 0;

            // Save to database on background thread to avoid blocking UI
            var savedSchedule = await Task.Run(async () =>
                await alarmScheduleService.AddScheduleAsync(dbSchedule, CancellationToken.None));

            Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleCreateScheduleAfterSavePublicationLanguage,
                savedSchedule.BiblePublicationSchedule?.PublicationCode ?? "null",
                savedSchedule.BiblePublicationSchedule?.LanguageCode ?? "null");

            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleCreateScheduleSavedToDb, savedSchedule.Id);

            // Create alarm if enabled (on background thread)
            if (savedSchedule.IsEnabled && alarmService != null)
            {
                await Task.Run(() => alarmService.Create(savedSchedule));
            }

            // Map DB entity → domain model (ScheduleStateItem) on background thread
            var scheduleStateItem = await Task.Run(() =>
            {
                return mapper.Map<ScheduleStateItem>(savedSchedule);
            });

            // Populate display names from media index (single-schedule hydration).
            await scheduleDisplayNameService.PopulateDisplayNamesAsync(scheduleStateItem, savedSchedule);

            // Dispatch success action with DTO (display names preserved from state)
            dispatcher.Dispatch(new CreateScheduleSuccessAction(scheduleStateItem));

            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleCreateScheduleDispatchedCreateScheduleSuccessAction,
                scheduleStateItem.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ErrorInHandleCreateSchedule);
            if (action.Schedule != null)
            {
                dispatcher.Dispatch(new CreateScheduleFailureAction(action.Schedule, ex.Message));
            }
        }
    }
}

