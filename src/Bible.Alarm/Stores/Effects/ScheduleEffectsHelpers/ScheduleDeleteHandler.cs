#nullable enable

using AutoMapper;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.Services;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

/// <summary>
/// Handles DeleteScheduleAction effect logic.
/// </summary>
public class ScheduleDeleteHandler
{
    private readonly IMapper mapper;
    private readonly IAlarmScheduleService? alarmScheduleService;
    private readonly IAlarmService? alarmService;
    private readonly IMediaCacheService? mediaCacheService;
    private readonly ScheduleDisplayNamePopulator displayNamePopulator;
    private readonly ScheduleCacheManager cacheManager;

    public ScheduleDeleteHandler(
        IMapper mapper,
        IAlarmScheduleService? alarmScheduleService,
        IAlarmService? alarmService,
        IMediaCacheService? mediaCacheService,
        ScheduleDisplayNamePopulator displayNamePopulator,
        ScheduleCacheManager cacheManager)
    {
        this.mapper = mapper;
        this.alarmScheduleService = alarmScheduleService;
        this.alarmService = alarmService;
        this.mediaCacheService = mediaCacheService;
        this.displayNamePopulator = displayNamePopulator;
        this.cacheManager = cacheManager;
    }

    public async Task HandleAsync(DeleteScheduleAction action, IDispatcher dispatcher)
    {
        try
        {
            Log.Information("ScheduleDeleteHandler: HandleAsync called - ScheduleId: {ScheduleId}, Action null: {IsNull}, Dispatcher null: {DispatcherNull}", 
                action?.ScheduleId ?? -1, action == null, dispatcher == null);
            
            if (action == null)
            {
                Log.Error("ScheduleDeleteHandler: HandleAsync - Action is null!");
                return;
            }
            
            if (dispatcher == null)
            {
                Log.Error("ScheduleDeleteHandler: HandleAsync - Dispatcher is null!");
                return;
            }
            
            Log.Information("ScheduleEffects: HandleDeleteSchedule - ScheduleId: {ScheduleId}", action.ScheduleId);

            if (alarmScheduleService == null)
            {
                Log.Warning("ScheduleEffects: HandleDeleteSchedule - Service unavailable, skipping");
                dispatcher.Dispatch(new DeleteScheduleFailureAction(action.ScheduleId, "Service unavailable"));
                return;
            }

            // Check if this is the last schedule - prevent deletion if it is (on background thread)
            var allSchedules = await Task.Run(async () => 
                await alarmScheduleService.GetAllSchedulesAsync(
                    includeMusic: false,
                    includeBibleReading: false,
                    CancellationToken.None));

            if (allSchedules.Count <= 1)
            {
                Log.Warning("ScheduleEffects: HandleDeleteSchedule - Cannot delete schedule {ScheduleId} - it is the last schedule", action.ScheduleId);
                // Show toast message to user
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
                            includeBibleReading: true,
                            CancellationToken.None);

                        if (scheduleFromDb != null)
                        {
                            var mapped = mapper.Map<ScheduleStateItem>(scheduleFromDb);
                            await displayNamePopulator.PopulateTranslationNameAsync(mapped, scheduleFromDb);
                            await displayNamePopulator.PopulateBookNameAsync(mapped, scheduleFromDb);
                            return mapped;
                        }
                        return null;
                    });
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "ScheduleEffects: HandleDeleteSchedule - Failed to load schedule for rollback, ScheduleId: {ScheduleId}", action.ScheduleId);
                }

                dispatcher.Dispatch(new DeleteScheduleFailureAction(action.ScheduleId, "Cannot delete last schedule", scheduleToRestore));
                return;
            }

            // Clear cache BEFORE delete to prevent stale cache if process crashes
            cacheManager.InvalidateScheduleCache();

            // Delete cached media files for this schedule (on background thread)
            if (mediaCacheService != null)
            {
                await Task.Run(async () => await mediaCacheService.DeleteScheduleCacheAsync(action.ScheduleId));
            }

            // Delete alarm notification (on background thread)
            if (alarmService != null)
            {
                await Task.Run(() => alarmService.Delete(action.ScheduleId));
            }

            // Delete from database (on background thread)
            await Task.Run(async () => 
                await alarmScheduleService.DeleteScheduleAsync(action.ScheduleId, CancellationToken.None));

            Log.Information("ScheduleEffects: HandleDeleteSchedule - Deleted from DB. ScheduleId: {ScheduleId}", action.ScheduleId);

            // Dispatch success action with schedule ID
            dispatcher.Dispatch(new RemoveScheduleSuccessAction(action.ScheduleId));

            Log.Information("ScheduleEffects: HandleDeleteSchedule - Dispatched RemoveScheduleSuccessAction for ScheduleId: {ScheduleId}",
                action.ScheduleId);

            // Refresh cache in background after successful delete
            _ = Task.Run(async () => await cacheManager.RefreshScheduleCacheAsync());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error in HandleDeleteSchedule");
            dispatcher.Dispatch(new DeleteScheduleFailureAction(action.ScheduleId, ex.Message));
        }
    }
}

