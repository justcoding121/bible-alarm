#nullable enable
using AutoMapper;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;
using Bible.Alarm.Stores.Effects.Services;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    IScheduleDisplayNameService? scheduleDisplayNameService = null)
{
    private readonly IMapper mapper = mapper;
    private readonly IAlarmScheduleService? alarmScheduleService = alarmScheduleService ?? ServiceProviderManager.GetService<IAlarmScheduleService>();
    private readonly IAlarmService? alarmService = alarmService ?? ServiceProviderManager.GetService<IAlarmService>();
    private readonly IMediaCacheService? mediaCacheService = mediaCacheService ?? ServiceProviderManager.GetService<IMediaCacheService>();
    private readonly IState<ApplicationState>? state = state ?? ServiceProviderManager.GetService<IState<ApplicationState>>();
    private readonly IScheduleDisplayNameService scheduleDisplayNameService = scheduleDisplayNameService ?? ServiceProviderManager.GetService<IScheduleDisplayNameService>()!;

    // Helper classes for modular functionality
    private readonly ScheduleUpdateProcessor updateProcessor = new(
        mapper,
        alarmScheduleService,
        alarmService,
        scheduleDisplayNameService: scheduleDisplayNameService,
        mediaService: mediaService);
    private readonly TrackSelectionSyncHandler trackSyncHandler = new(state);

    // Effect handlers - initialized lazily when first accessed
    private ScheduleAddHandler? _addHandler;
    private ScheduleUpdateHandler? _updateHandler;
    private ScheduleCreateHandler? _createHandler;
    private ScheduleDeleteHandler? _deleteHandler;
    private ScheduleSuccessHandler? _successHandler;

    private ScheduleAddHandler addHandler => _addHandler ??= new ScheduleAddHandler(mapper, scheduleDisplayNameService);
    private ScheduleUpdateHandler updateHandler => _updateHandler ??= new ScheduleUpdateHandler(mapper, scheduleDisplayNameService);
    private ScheduleCreateHandler createHandler => _createHandler ??= new ScheduleCreateHandler(mapper, this.alarmScheduleService, this.alarmService, scheduleDisplayNameService);
    private ScheduleDeleteHandler deleteHandler => _deleteHandler ??= new ScheduleDeleteHandler(mapper, this.alarmScheduleService, this.alarmService, this.mediaCacheService, scheduleDisplayNameService);
    private ScheduleSuccessHandler successHandler => _successHandler ??= new ScheduleSuccessHandler();

    [EffectMethod]
    public async Task HandleViewSchedule(ViewScheduleAction action, IDispatcher dispatcher)
    {
        try
        {
            var currentState = state ?? ServiceProviderManager.GetService<IState<ApplicationState>>()!;
            var currentSchedule = currentState.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                return;
            }

            await TryDispatchModalCountsUpdateAsync(currentSchedule, dispatcher, reason: "ViewScheduleAction");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error in HandleViewSchedule (modal counts)");
        }
    }

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
                
                // Even when not saving, we may need to populate missing display names (e.g., section name from track number)
                // This is especially important when switching music types where track number is preserved but section name is missing
                // Music type is inferred from LanguageCode: null/empty = melody (instrumental), otherwise = vocal
                var isMelodyMusic = string.IsNullOrEmpty(action.Schedule.MusicLanguageCode);
                if (action.MusicUpdated && isMelodyMusic &&
                    !string.IsNullOrWhiteSpace(action.Schedule.MusicTrackCode) &&
                    string.IsNullOrWhiteSpace(action.Schedule.MusicSectionName) &&
                    !string.IsNullOrWhiteSpace(action.Schedule.MusicPublicationCode))
                {
                    Log.Debug("ScheduleEffects: HandleUpdateScheduleFromViewModel - Populating MusicSectionName from track for Instrumental. PublicationCode={PublicationCode}, TrackCode={TrackCode}",
                        action.Schedule.MusicPublicationCode, action.Schedule.MusicTrackCode);
                    var sf = ServiceProviderManager.GetService<IServiceScopeFactory>();
                    if (sf != null)
                    {
                        await ScheduleEffectsMusicSectionPopulator.PopulateMusicSectionNameForStateAsync(action.Schedule, dispatcher, sf, Log.Logger);
                    }
                }
                
                return;
            }

            if (alarmScheduleService == null)
            {
                updateProcessor.HandleServiceUnavailable(action, dispatcher);
                return;
            }

            var savedSchedule = await updateProcessor.UpdateScheduleInDatabaseAsync(action);
            await updateProcessor.UpdateAlarmAsync(savedSchedule);

            var scheduleStateItem = await updateProcessor.MapAndPreserveDisplayNames(action, savedSchedule);
            dispatcher.Dispatch(new UpdateScheduleSuccessAction(scheduleStateItem));

            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Dispatched UpdateScheduleSuccessAction for ScheduleId: {ScheduleId}, scheduleStateItem.MusicLanguageCode={LanguageCode}",
                scheduleStateItem.Id, scheduleStateItem.MusicLanguageCode ?? "null");

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

    [EffectMethod]
    public async Task HandleUpdateScheduleFromViewModelPopulateModalCounts(UpdateScheduleFromViewModelAction action, IDispatcher dispatcher)
    {
        try
        {
            // Only compute/refresh discovery-based modal counts during live edits (never during saves).
            if (action.ShouldSave)
            {
                return;
            }

            var currentState = state ?? ServiceProviderManager.GetService<IState<ApplicationState>>()!;
            var currentSchedule = currentState.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                return;
            }

            // Ensure we update the currently edited schedule only.
            if (currentSchedule.Id != action.Schedule.Id)
            {
                return;
            }

            // Check if music is enabled and has a publication (needs modal counts refresh)
            var musicNeedsModalCounts = currentSchedule.MusicEnabled && 
                                       !string.IsNullOrWhiteSpace(currentSchedule.MusicPublicationCode) &&
                                       (!currentSchedule.MusicPublicationModalItemCount.HasValue || 
                                        !currentSchedule.MusicSectionModalItemCount.HasValue);

            // Only run when Bible/Music selection changed (call sites should set these flags accurately),
            // OR when music is enabled but modal counts are missing (to refresh modal counts for existing music selection).
            if (!action.BiblePublicationUpdated && !action.MusicUpdated && !musicNeedsModalCounts)
            {
                return;
            }

            Log.Debug("ScheduleEffects: HandleUpdateScheduleFromViewModelPopulateModalCounts - Refreshing modal counts. Reason: BibleUpdated={BibleUpdated}, MusicUpdated={MusicUpdated}, MusicNeedsModalCounts={MusicNeedsModalCounts}",
                action.BiblePublicationUpdated, action.MusicUpdated, musicNeedsModalCounts);

            await TryDispatchModalCountsUpdateAsync(currentSchedule, dispatcher, reason: "UpdateScheduleFromViewModelAction");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error populating modal counts from UpdateScheduleFromViewModelAction");
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

            Log.Debug("ScheduleEffects: HandleDeleteSchedule - Calling deleteHandler.HandleAsync for ScheduleId: {ScheduleId}", action.ScheduleId);
            await deleteHandler.HandleAsync(action, dispatcher);
            Log.Debug("ScheduleEffects: HandleDeleteSchedule - deleteHandler.HandleAsync completed for ScheduleId: {ScheduleId}", action.ScheduleId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: HandleDeleteSchedule - Exception occurred! ScheduleId: {ScheduleId}", action?.ScheduleId ?? -1);
            // Re-throw to ensure Fluxor sees the error
            throw;
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
        try
        {
            var currentState = state ?? ServiceProviderManager.GetService<IState<ApplicationState>>()!;
            var currentSchedule = currentState.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                return;
            }

            await TryDispatchModalCountsUpdateAsync(currentSchedule, dispatcher, reason: "Bible TrackSelectedAction");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error in HandleBiblePublicationTrackSelected (modal counts)");
        }
    }

    /// <summary>
    /// Effect: Sync CurrentMusic to CurrentSchedule when TrackSelectedAction is dispatched.
    /// Dispatches UpdateScheduleFromViewModelAction (musicUpdated: true, shouldSave: false) so that
    /// HandleUpdateScheduleFromViewModel can run PopulateMusicSectionNameForStateAsync for instrumental music.
    /// </summary>
    [EffectMethod]
    public async Task HandleMusicTrackSelected(Bible.Alarm.Stores.Actions.Music.TrackSelectedAction action, IDispatcher dispatcher)
    {
        await trackSyncHandler.HandleTrackSelected(action, dispatcher);
    }

    [EffectMethod]
    public async Task HandleMusicSectionSelected(MusicSectionSelectedAction action, IDispatcher dispatcher)
    {
        try
        {
            var currentState = state ?? ServiceProviderManager.GetService<IState<ApplicationState>>()!;
            var currentSchedule = currentState.Value.CurrentSchedule;
            if (currentSchedule == null)
            {
                return;
            }

            await TryDispatchModalCountsUpdateAsync(currentSchedule, dispatcher, reason: "MusicSectionSelectedAction");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error in HandleMusicSectionSelected (modal counts)");
        }
    }

    private async Task TryDispatchModalCountsUpdateAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher, string reason)
    {
        try
        {
            var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>();
            if (scopeFactory == null)
            {
                Log.Warning("ScheduleEffects: Cannot populate modal counts - IServiceScopeFactory not available. ScheduleId={ScheduleId}", currentSchedule.Id);
                return;
            }

            var updatedSchedule = await ScheduleEffectsModalCountPopulator.TryPopulateModalCountsAsync(currentSchedule, scopeFactory, Log.Logger);
            if (updatedSchedule == null)
            {
                return;
            }

            if (ScheduleEffectsModalCountPopulator.AreModalCountsEquivalent(currentSchedule, updatedSchedule))
            {
                return;
            }

            Log.Debug("ScheduleEffects: Updating modal counts. Reason={Reason}, ScheduleId={ScheduleId}, BiblePubCount={BiblePubCount}, BibleSectionCount={BibleSectionCount}, MusicPubCount={MusicPubCount}, MusicSectionCount={MusicSectionCount}",
                reason,
                currentSchedule.Id,
                updatedSchedule.BiblePublicationModalItemCount,
                updatedSchedule.BiblePublicationSectionModalItemCount,
                updatedSchedule.MusicPublicationModalItemCount,
                updatedSchedule.MusicSectionModalItemCount);

            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, musicUpdated: false, biblePublicationUpdated: false, shouldSave: false));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error updating modal counts. Reason={Reason}, ScheduleId={ScheduleId}",
                reason, currentSchedule.Id);
        }
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

    /// <summary>
    /// Effect: Auto-populate cascade when Music fields are updated.
    /// Cascade order: MusicType → Language → Publication → Section → Track
    /// </summary>
    [EffectMethod]
    public async Task HandleMusicCascade(UpdateScheduleFromViewModelAction action, IDispatcher dispatcher)
    {
        try
        {
            // Only trigger if this is a music update
            if (!action.MusicUpdated)
            {
                Log.Debug("ScheduleEffects: HandleMusicCascade - Skipping, musicUpdated=false, ScheduleId={ScheduleId}", action.Schedule?.Id ?? 0);
                return;
            }

            Log.Debug("ScheduleEffects: HandleMusicCascade - Triggered, ScheduleId={ScheduleId}, MusicPublicationCode={PublicationCode}",
                action.Schedule?.Id ?? 0, action.Schedule?.MusicPublicationCode ?? "null");

            var currentState = state ?? ServiceProviderManager.GetService<IState<ApplicationState>>()!;
            var currentMediaService = mediaService ?? ServiceProviderManager.GetService<IMediaService>()!;
            var currentBiblePublicationService = BiblePublicationService ?? ServiceProviderManager.GetService<IBiblePublicationService>()!;
            var languageContentService = ServiceProviderManager.GetService<Bible.Alarm.Shared.Services.Media.Interfaces.ILanguageContentService>()!;
            var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>()!;
            
            var handler = new MusicCascadeHandler(
                currentBiblePublicationService,
                currentMediaService,
                languageContentService,
                currentState,
                scopeFactory,
                Log.Logger);
            
            await handler.HandleAsync(dispatcher);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error handling Music cascade");
        }
    }

}

