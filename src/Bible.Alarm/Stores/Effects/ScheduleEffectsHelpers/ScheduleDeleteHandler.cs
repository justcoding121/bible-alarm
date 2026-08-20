#nullable enable

using AutoMapper;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

public class ScheduleDeleteHandler
{
    private readonly IMapper mapper;
    private readonly IAlarmScheduleService? alarmScheduleService;
    private readonly IAlarmService? alarmService;
    private readonly IMediaCacheService? mediaCacheService;
    private readonly IScheduleDisplayNameService scheduleDisplayNameService;

    public ScheduleDeleteHandler(
        IMapper mapper,
        IAlarmScheduleService? alarmScheduleService,
        IAlarmService? alarmService,
        IMediaCacheService? mediaCacheService,
        IScheduleDisplayNameService scheduleDisplayNameService)
    {
        this.mapper = mapper;
        this.alarmScheduleService = alarmScheduleService;
        this.alarmService = alarmService;
        this.mediaCacheService = mediaCacheService;
        this.scheduleDisplayNameService = scheduleDisplayNameService;
    }

    public async Task HandleAsync(DeleteScheduleAction action, IDispatcher dispatcher)
    {
        try
        {
            Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ScheduleDeleteHandlerHandleAsyncCalled,
                action?.ScheduleId ?? -1, action == null, dispatcher == null);

            if (action == null)
            {
                Log.Error(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ScheduleDeleteHandlerHandleAsyncActionIsNull);
                return;
            }

            if (dispatcher == null)
            {
                Log.Error(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ScheduleDeleteHandlerHandleAsyncDispatcherIsNull);
                return;
            }

            Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleDeleteScheduleScheduleId, action.ScheduleId);

            if (alarmScheduleService == null)
            {
                Log.Warning(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleDeleteScheduleServiceUnavailableSkipping);
                dispatcher.Dispatch(new DeleteScheduleFailureAction(action.ScheduleId, "Service unavailable"));
                return;
            }

            // Check if this is the last schedule - prevent deletion if it is (on background thread)
            var allSchedules = await Task.Run(async () =>
                await alarmScheduleService.GetAllSchedulesAsync(
                    includeMusic: false,
                    includeBiblePublication: false,
                    CancellationToken.None));

            if (allSchedules.Count <= 1)
            {
                Log.Warning(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleDeleteScheduleCannotDeleteLastSchedule, action.ScheduleId);
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Cannot delete last schedule"));

                // Load the schedule from DB to restore it in the reducer (on background thread)
                ScheduleStateItem? scheduleToRestore = null;
                try
                {
                    scheduleToRestore = await Task.Run(async () =>
                    {
                        var scheduleFromDb = await alarmScheduleService.GetScheduleByIdAsync(
                            action.ScheduleId,
                            includeMusic: true,
                            includeBiblePublication: true,
                            CancellationToken.None);

                        if (scheduleFromDb != null)
                        {
                            var mapped = mapper.Map<ScheduleStateItem>(scheduleFromDb);
                            await scheduleDisplayNameService.PopulateDisplayNamesAsync(mapped, scheduleFromDb);
                            return mapped;
                        }
                        return null;
                    });
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleDeleteScheduleFailedToLoadScheduleForRollback, action.ScheduleId);
                }

                dispatcher.Dispatch(new DeleteScheduleFailureAction(action.ScheduleId, "Cannot delete last schedule", scheduleToRestore));
                return;
            }

            if (mediaCacheService != null)
            {
                await Task.Run(async () => await mediaCacheService.DeleteScheduleCacheAsync(action.ScheduleId));
            }

            if (alarmService != null)
            {
                await alarmService.Delete(action.ScheduleId);
            }

            await Task.Run(async () =>
                await alarmScheduleService.DeleteScheduleAsync(action.ScheduleId, CancellationToken.None));

            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleDeleteScheduleDeletedFromDb, action.ScheduleId);

            WeakReferenceMessenger.Default.Send(new ShowToastMessage("Schedule removed"));

            dispatcher.Dispatch(new RemoveScheduleSuccessAction(action.ScheduleId));

            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleDeleteScheduleDispatchedRemoveScheduleSuccessAction,
                action.ScheduleId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ErrorInHandleDeleteSchedule);
            dispatcher.Dispatch(new DeleteScheduleFailureAction(action.ScheduleId, ex.Message));
        }
    }
}

