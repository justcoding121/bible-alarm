#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

/// <summary>
/// Handles UpdateScheduleAction effect logic.
/// </summary>
public class ScheduleUpdateHandler
{
    private readonly IMapper mapper;
    private readonly IScheduleDisplayNameService scheduleDisplayNameService;

    public ScheduleUpdateHandler(IMapper mapper, IScheduleDisplayNameService scheduleDisplayNameService)
    {
        this.mapper = mapper;
        this.scheduleDisplayNameService = scheduleDisplayNameService;
    }

    public async Task HandleAsync(UpdateScheduleAction action, IDispatcher dispatcher)
    {
        try
        {
            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleScheduleIdName,
                action.Schedule?.Id, action.Schedule?.Name);

            if (action.Schedule == null)
            {
                Log.Warning(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleScheduleNullSkipping);
                return;
            }

            // Transform DB entity to State DTO
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(action.Schedule);

            // Populate display names from media index (single-schedule hydration).
            await scheduleDisplayNameService.PopulateDisplayNamesAsync(scheduleStateItem, action.Schedule);

            // Dispatch success action with DTO (reducer will handle this)
            dispatcher.Dispatch(new UpdateScheduleSuccessAction(scheduleStateItem));

            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleDispatchedUpdateScheduleSuccessForScheduleId,
                scheduleStateItem.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ErrorInHandleUpdateSchedule);
        }
    }
}

