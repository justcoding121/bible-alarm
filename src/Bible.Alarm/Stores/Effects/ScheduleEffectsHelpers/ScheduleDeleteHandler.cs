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
            Log.Information("ScheduleEffects: HandleDeleteSchedule - ScheduleId: {ScheduleId}", action.ScheduleId);

            if (alarmScheduleService == null)
            {
                Log.Warning("ScheduleEffects: HandleDeleteSchedule - Service unavailable, skipping");
                dispatcher.Dispatch(new DeleteScheduleFailureAction(action.ScheduleId, "Service unavailable"));
                return;
            }

            // Check if this is the last schedule - prevent deletion if it is
            var allSchedules = await alarmScheduleService.GetAllSchedulesAsync(
                includeMusic: false,
                includeBibleReading: false,
                CancellationToken.None);

            if (allSchedules.Count <= 1)
            {
                Log.Warning("ScheduleEffects: HandleDeleteSchedule - Cannot delete schedule {ScheduleId} - it is the last schedule", action.ScheduleId);
                // Show toast message to user
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Cannot delete last schedule"));

                // Load the schedule from DB to restore it in the reducer
                ScheduleStateItem? scheduleToRestore = null;
                try
                {
                    var scheduleFromDb = await alarmScheduleService.GetScheduleByIdAsync(
                        action.ScheduleId,
                        includeMusic: true,
                        includeBibleReading: true,
                        CancellationToken.None);

                    if (scheduleFromDb != null)
                    {
                        scheduleToRestore = mapper.Map<ScheduleStateItem>(scheduleFromDb);
                        await displayNamePopulator.PopulateTranslationNameAsync(scheduleToRestore, scheduleFromDb);
                        await displayNamePopulator.PopulateBookNameAsync(scheduleToRestore, scheduleFromDb);
                    }
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

            // Delete cached media files for this schedule
            if (mediaCacheService != null)
            {
                await mediaCacheService.DeleteScheduleCacheAsync(action.ScheduleId);
            }

            // Delete alarm notification
            if (alarmService != null)
            {
                await Task.Run(() => alarmService.Delete(action.ScheduleId));
            }

            // Delete from database
            await alarmScheduleService.DeleteScheduleAsync(action.ScheduleId, CancellationToken.None);

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

