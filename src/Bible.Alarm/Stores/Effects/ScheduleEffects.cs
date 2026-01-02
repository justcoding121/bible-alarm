#nullable enable
using AutoMapper;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.Services;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Stores.Effects;

/// <summary>
/// Effects handle async side-effects and DB/API model → State DTO transformations.
/// Following Fluxor best practices: Effects transform data, Reducers are pure.
/// 
/// For saves (Create/Update/Delete):
/// - Map domain model (ScheduleStateItem) → DB entity (AlarmSchedule)
/// - Perform DB operations
/// - Map DB entity → domain model
/// - Dispatch success/failure actions
/// </summary>
public class ScheduleEffects(
    IMapper mapper,
    IBibleTranslationService? bibleTranslationService = null,
    IBibleBookService? bibleBookService = null,
    IAlarmScheduleService? alarmScheduleService = null,
    IAlarmService? alarmService = null,
    IMediaCacheService? mediaCacheService = null,
    IMediaService? mediaService = null,
    IState<ApplicationState>? state = null,
    IDiskCacheService? diskCacheService = null)
{
    private readonly IMapper mapper = mapper;
    private readonly IAlarmScheduleService? alarmScheduleService = alarmScheduleService ?? ServiceProviderManager.GetService<IAlarmScheduleService>();
    private readonly IAlarmService? alarmService = alarmService ?? ServiceProviderManager.GetService<IAlarmService>();
    private readonly IMediaCacheService? mediaCacheService = mediaCacheService ?? ServiceProviderManager.GetService<IMediaCacheService>();
    private readonly IState<ApplicationState>? state = state ?? ServiceProviderManager.GetService<IState<ApplicationState>>();

    // Helper classes for modular functionality
    private readonly ScheduleDisplayNamePopulator displayNamePopulator = new(
        bibleTranslationService,
        bibleBookService,
        mediaService);
    private readonly ScheduleCacheManager cacheManager = new(diskCacheService);
    private readonly ScheduleUpdateProcessor updateProcessor = new(
        mapper,
        alarmScheduleService,
        alarmService);
    private readonly ChapterSelectionSyncHandler chapterSyncHandler = new(state);
    private readonly TrackSelectionSyncHandler trackSyncHandler = new(state);

    // Effect handlers - initialized lazily when first accessed
    private ScheduleAddHandler? _addHandler;
    private ScheduleUpdateHandler? _updateHandler;
    private ScheduleCreateHandler? _createHandler;
    private ScheduleDeleteHandler? _deleteHandler;
    private ScheduleSuccessHandler? _successHandler;

    private ScheduleAddHandler addHandler => _addHandler ??= new ScheduleAddHandler(mapper, displayNamePopulator);
    private ScheduleUpdateHandler updateHandler => _updateHandler ??= new ScheduleUpdateHandler(mapper, displayNamePopulator, cacheManager);
    private ScheduleCreateHandler createHandler => _createHandler ??= new ScheduleCreateHandler(mapper, this.alarmScheduleService, this.alarmService, cacheManager);
    private ScheduleDeleteHandler deleteHandler => _deleteHandler ??= new ScheduleDeleteHandler(mapper, this.alarmScheduleService, this.alarmService, this.mediaCacheService, displayNamePopulator, cacheManager);
    private ScheduleSuccessHandler successHandler => _successHandler ??= new ScheduleSuccessHandler(cacheManager);

    /// <summary>
    /// Effect: Transform DB entity to DTO and dispatch success action.
    /// Called when AddScheduleAction is dispatched with a DB entity.
    /// </summary>
    [EffectMethod]
    public async Task HandleAddSchedule(AddScheduleAction action, IDispatcher dispatcher)
    {
        await addHandler.HandleAsync(action, dispatcher);
    }

    /// <summary>
    /// Effect: Transform DB entity to DTO and dispatch success action.
    /// Called when UpdateScheduleAction is dispatched with a DB entity.
    /// </summary>
    [EffectMethod]
    public async Task HandleUpdateSchedule(UpdateScheduleAction action, IDispatcher dispatcher)
    {
        await updateHandler.HandleAsync(action, dispatcher);
    }

    /// <summary>
    /// Effect: Extract schedule ID and dispatch success action.
    /// Called when RemoveScheduleAction is dispatched with a DB entity.
    /// </summary>
    [EffectMethod]
    public Task HandleRemoveSchedule(RemoveScheduleAction action, IDispatcher dispatcher)
    {
        try
        {
            Log.Information("ScheduleEffects: HandleRemoveSchedule - ScheduleId: {ScheduleId}",
                action.Schedule?.Id);

            if (action.Schedule == null)
            {
                Log.Warning("ScheduleEffects: HandleRemoveSchedule - Schedule is null, skipping");
                return Task.CompletedTask;
            }

            // Dispatch success action with schedule ID (reducer will handle this)
            dispatcher.Dispatch(new RemoveScheduleSuccessAction(action.Schedule.Id));

            Log.Information("ScheduleEffects: HandleRemoveSchedule - Dispatched RemoveScheduleSuccessAction for ScheduleId: {ScheduleId}",
                action.Schedule.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error in HandleRemoveSchedule");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Effect: Handle CreateScheduleAction - Map DTO → DB entity, save to DB, map back → DTO, dispatch success/failure.
    /// Following Fluxor best practices: Effects handle DB operations and mapping.
    /// </summary>
    [EffectMethod]
    public async Task HandleCreateSchedule(CreateScheduleAction action, IDispatcher dispatcher)
    {
        await createHandler.HandleAsync(action, dispatcher);
    }

    /// <summary>
    /// Effect: Handle UpdateScheduleFromViewModelAction - Map DTO → DB entity, update DB, map back → DTO, dispatch success/failure.
    /// Following Fluxor best practices: Effects handle DB operations and mapping.
    /// </summary>
    [EffectMethod]
    public async Task HandleUpdateScheduleFromViewModel(UpdateScheduleFromViewModelAction action, IDispatcher dispatcher)
    {
        try
        {
            updateProcessor.LogUpdateStart(action);

            if (action.Schedule == null)
            {
                Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - Schedule is null, skipping");
                return;
            }

            if (!action.ShouldSave)
            {
                Log.Debug("ScheduleEffects: HandleUpdateScheduleFromViewModel - ShouldSave=false, skipping DB update. Only state was updated.");
                return;
            }

            if (alarmScheduleService == null)
            {
                updateProcessor.HandleServiceUnavailable(action, dispatcher);
                return;
            }

            // Clear cache BEFORE save to prevent stale cache if process crashes
            cacheManager.InvalidateScheduleCache();

            var savedSchedule = await updateProcessor.UpdateScheduleInDatabaseAsync(action);
            await updateProcessor.UpdateAlarmAsync(savedSchedule);

            var scheduleStateItem = updateProcessor.MapAndPreserveDisplayNames(action, savedSchedule);
            dispatcher.Dispatch(new UpdateScheduleSuccessAction(scheduleStateItem));

            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Dispatched UpdateScheduleSuccessAction for ScheduleId: {ScheduleId}, scheduleStateItem.MusicType={MusicType}",
                scheduleStateItem.Id, scheduleStateItem.MusicType?.ToString() ?? "null");

            // Note: Cache invalidation is handled by HandleUpdateScheduleSuccess effect to avoid duplication
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error in HandleUpdateScheduleFromViewModel");
            if (action.Schedule != null)
            {
                dispatcher.Dispatch(new UpdateScheduleFailureAction(action.Schedule, ex.Message));
            }
        }
    }

    /// <summary>
    /// Effect: Handle DeleteScheduleAction - Delete from DB, dispatch success/failure.
    /// Following Fluxor best practices: Effects handle DB operations.
    /// </summary>
    [EffectMethod]
    public async Task HandleDeleteSchedule(DeleteScheduleAction action, IDispatcher dispatcher)
    {
        await deleteHandler.HandleAsync(action, dispatcher);
    }

    /// <summary>
    /// Effect: Handle UpdateScheduleSuccessAction - Invalidate cache when a schedule is updated in the database.
    /// This covers chapter navigation, enable/disable toggle, track changes, and other schedule updates.
    /// </summary>
    [EffectMethod]
    public Task HandleUpdateScheduleSuccess(UpdateScheduleSuccessAction action, IDispatcher dispatcher)
    {
        return successHandler.HandleUpdateScheduleSuccess(action, dispatcher);
    }

    /// <summary>
    /// Effect: Handle RemoveScheduleSuccessAction - Invalidate cache when a schedule is deleted from the database.
    /// Also refreshes last played metadata if deleted schedule was the last played item and refreshes Android Auto.
    /// </summary>
    [EffectMethod]
    public async Task HandleRemoveScheduleSuccess(RemoveScheduleSuccessAction action, IDispatcher dispatcher)
    {
        await successHandler.HandleRemoveScheduleSuccess(action, dispatcher);
    }

    /// <summary>
    /// Effect: Sync CurrentBibleReadingSchedule to CurrentSchedule when ChapterSelectedAction is dispatched.
    /// This ensures that when sub-pages update CurrentBibleReadingSchedule, CurrentSchedule is also updated
    /// so the schedule page displays the changes immediately.
    /// </summary>
    [EffectMethod]
    public async Task HandleChapterSelected(ChapterSelectedAction action, IDispatcher dispatcher)
    {
        await chapterSyncHandler.HandleChapterSelected(action, dispatcher);
    }

    /// <summary>
    /// Effect: Sync CurrentMusic to CurrentSchedule when TrackSelectedAction is dispatched.
    /// This ensures that when sub-pages update CurrentMusic, CurrentSchedule is also updated
    /// so the schedule page displays the changes immediately.
    /// </summary>
    [EffectMethod]
    public async Task HandleTrackSelected(TrackSelectedAction action, IDispatcher dispatcher)
    {
        await trackSyncHandler.HandleTrackSelected(action, dispatcher);
    }
}

