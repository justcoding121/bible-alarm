#nullable enable
using AutoMapper;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;
using Bible.Alarm.Stores.Effects.Services;
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
    IBiblePublicationService? BiblePublicationService = null,
    IBiblePublicationSectionService? biblePublicationSectionService = null,
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
        BiblePublicationService,
        biblePublicationSectionService,
        mediaService);
    private readonly ScheduleCacheManager cacheManager = new(diskCacheService);
    private readonly ScheduleUpdateProcessor updateProcessor = new(
        mapper,
        alarmScheduleService,
        alarmService);
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

            var scheduleStateItem = await updateProcessor.MapAndPreserveDisplayNames(action, savedSchedule);
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
        try
        {
            Log.Information("ScheduleEffects: HandleDeleteSchedule Effect method called - ScheduleId: {ScheduleId}", action.ScheduleId);

            if (action == null)
            {
                Log.Error("ScheduleEffects: HandleDeleteSchedule - Action is null!");
                return;
            }

            if (dispatcher == null)
            {
                Log.Error("ScheduleEffects: HandleDeleteSchedule - Dispatcher is null!");
                return;
            }

            Log.Debug("ScheduleEffects: HandleDeleteSchedule - Calling deleteHandler.HandleAsync for ScheduleId: {ScheduleId}", action.ScheduleId);
            await deleteHandler.HandleAsync(action, dispatcher);
            Log.Debug("ScheduleEffects: HandleDeleteSchedule - deleteHandler.HandleAsync completed for ScheduleId: {ScheduleId}", action.ScheduleId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: HandleDeleteSchedule - Exception occurred! ScheduleId: {ScheduleId}", action?.ScheduleId ?? -1);
            throw; // Re-throw to ensure Fluxor sees the error
        }
    }

    /// <summary>
    /// Effect: Handle UpdateScheduleSuccessAction - Invalidate cache when a schedule is updated in the database.
    /// This covers track navigation, enable/disable toggle, track changes, and other schedule updates.
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
    /// Effect: Sync CurrentBiblePublicationSchedule to CurrentSchedule when TrackSelectedAction is dispatched.
    /// This ensures that when sub-pages update CurrentBiblePublicationSchedule, CurrentSchedule is also updated
    /// so the schedule page displays the changes immediately.
    /// </summary>
    [EffectMethod]
    public async Task HandleBiblePublicationTrackSelected(Bible.Alarm.Stores.Actions.BiblePublications.TrackSelectedAction action, IDispatcher dispatcher)
    {
        // No-op: Reducer OnBiblePublicationTrackSelected now directly updates CurrentSchedule
        // No sync needed - CurrentSchedule is the single source of truth
        await Task.CompletedTask;
    }

    /// <summary>
    /// Effect: Sync CurrentMusic to CurrentSchedule when TrackSelectedAction is dispatched.
    /// This ensures that when sub-pages update CurrentMusic, CurrentSchedule is also updated
    /// so the schedule page displays the changes immediately.
    /// </summary>
    [EffectMethod]
    public async Task HandleMusicTrackSelected(Bible.Alarm.Stores.Actions.Music.TrackSelectedAction action, IDispatcher dispatcher)
    {
        // No-op: Reducer OnMusicTrackSelected now directly updates CurrentSchedule
        // No sync needed - CurrentSchedule is the single source of truth
        await Task.CompletedTask;
    }

    /// <summary>
    /// Effect: Auto-populate schedule when category is selected.
    /// Finds first language and publication for the category, harvests if needed, and populates schedule.
    /// </summary>
    [EffectMethod]
    public async Task HandleCategorySelection(Bible.Alarm.Stores.Actions.BiblePublications.CategorySelectionAction action, IDispatcher dispatcher)
    {
        try
        {
            var currentState = state ?? ServiceProviderManager.GetService<IState<ApplicationState>>()!;
            var currentMediaService = mediaService ?? ServiceProviderManager.GetService<IMediaService>()!;
            var currentBiblePublicationService = BiblePublicationService ?? ServiceProviderManager.GetService<IBiblePublicationService>()!;
            var currentBiblePublicationSectionService = biblePublicationSectionService ?? ServiceProviderManager.GetService<IBiblePublicationSectionService>();
            var languageContentService = ServiceProviderManager.GetService<Bible.Alarm.Shared.Services.Media.Interfaces.ILanguageContentService>()!;
            var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>()!;
            
            var handler = new CategorySelectionAutoPopulateHandler(
                currentBiblePublicationService,
                currentMediaService,
                languageContentService,
                new Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers.BiblePublicationSelectionItemSelector(
                    currentMediaService,
                    currentState,
                    currentBiblePublicationService,
                    currentBiblePublicationSectionService,
                    languageContentService,
                    scopeFactory),
                currentState,
                scopeFactory,
                Log.Logger);
            
            await handler.HandleAsync(action, dispatcher);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error handling category selection for category={CategoryName}", action.CategoryName);
        }
    }

    /// <summary>
    /// Effect: Auto-populate cascade when Bible publication fields are updated.
    /// Cascade order: Language → Publication → Section → Track
    /// </summary>
    [EffectMethod]
    public async Task HandleBiblePublicationCascade(UpdateScheduleFromViewModelAction action, IDispatcher dispatcher)
    {
        try
        {
            // Only trigger if this is a bible publication update
            if (!action.BiblePublicationUpdated)
            {
                return;
            }

            var currentState = state ?? ServiceProviderManager.GetService<IState<ApplicationState>>()!;
            var currentMediaService = mediaService ?? ServiceProviderManager.GetService<IMediaService>()!;
            var currentBiblePublicationService = BiblePublicationService ?? ServiceProviderManager.GetService<IBiblePublicationService>()!;
            var currentBiblePublicationSectionService = biblePublicationSectionService ?? ServiceProviderManager.GetService<IBiblePublicationSectionService>();
            var languageContentService = ServiceProviderManager.GetService<Bible.Alarm.Shared.Services.Media.Interfaces.ILanguageContentService>()!;
            var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>()!;
            
            var handler = new BiblePublicationCascadeHandler(
                currentBiblePublicationService,
                currentMediaService,
                languageContentService,
                new Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers.BiblePublicationSelectionItemSelector(
                    currentMediaService,
                    currentState,
                    currentBiblePublicationService,
                    currentBiblePublicationSectionService,
                    languageContentService,
                    scopeFactory),
                currentState,
                scopeFactory,
                Log.Logger);
            
            await handler.HandleAsync(dispatcher);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error handling Bible publication cascade");
        }
    }
}

