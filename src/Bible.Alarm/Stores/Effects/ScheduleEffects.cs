#nullable enable
using AutoMapper;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
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
                    await PopulateMusicSectionNameForStateAsync(action.Schedule, dispatcher);
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
            var updatedSchedule = await TryPopulateModalCountsAsync(currentSchedule);
            if (updatedSchedule == null)
            {
                return;
            }

            if (AreModalCountsEquivalent(currentSchedule, updatedSchedule))
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

    private static bool AreModalCountsEquivalent(ScheduleStateItem a, ScheduleStateItem b)
    {
        return a.BiblePublicationModalItemCount == b.BiblePublicationModalItemCount &&
               a.BiblePublicationSectionModalItemCount == b.BiblePublicationSectionModalItemCount &&
               a.MusicPublicationModalItemCount == b.MusicPublicationModalItemCount &&
               a.MusicSectionModalItemCount == b.MusicSectionModalItemCount;
    }


    private async Task<ScheduleStateItem?> TryPopulateModalCountsAsync(ScheduleStateItem currentSchedule)
    {
        try
        {
            var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>();
            if (scopeFactory == null)
            {
                Log.Warning("ScheduleEffects: Cannot populate modal counts - IServiceScopeFactory not available. ScheduleId={ScheduleId}", currentSchedule.Id);
                return null;
            }

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var updated = currentSchedule.DeepClone();

            updated.BiblePublicationModalItemCount = await GetBiblePublicationModalItemCountAsync(db, updated);
            updated.BiblePublicationSectionModalItemCount = await GetBiblePublicationSectionModalItemCountAsync(db, updated);

            if (updated.MusicEnabled && HasMusicConfigured(updated))
            {
                updated.MusicPublicationModalItemCount = await GetMusicPublicationModalItemCountAsync(db, updated);
                updated.MusicSectionModalItemCount = await GetMusicSectionModalItemCountAsync(db, updated);
            }
            else
            {
                // Keep existing values if any; don't force nulls while Music is disabled/uninitialized.
                updated.MusicPublicationModalItemCount ??= currentSchedule.MusicPublicationModalItemCount;
                updated.MusicSectionModalItemCount ??= currentSchedule.MusicSectionModalItemCount;
            }

            return updated;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error populating modal counts. ScheduleId={ScheduleId}", currentSchedule.Id);
            return null;
        }
    }

    private static bool HasMusicConfigured(ScheduleStateItem schedule)
    {
        return !string.IsNullOrWhiteSpace(schedule.MusicPublicationCode);
    }

    /// <summary>
    /// Publication modal count for Bible row. Uses category from schedule (Bible/Dramas/etc.).
    /// Music row uses the same query shape with category fixed to "Music" (see GetMusicPublicationModalItemCountAsync in ScheduleEffects and MusicCascadeHandler).
    /// </summary>
    private static async Task<int?> GetBiblePublicationModalItemCountAsync(MediaDbContext db, ScheduleStateItem schedule)
    {
        var categoryName = schedule.BiblePublicationCategoryName;
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return null;
        }

        var languageCode = schedule.BiblePublicationLanguageCode;
        var normalizedLanguageCode = string.IsNullOrWhiteSpace(languageCode) ? null : languageCode.ToUpperInvariant();

        var query = db.PublicationLanguages
            .AsNoTracking()
            .Where(pl => pl.Category != null && pl.Category.CategoryName == categoryName);

        if (!string.IsNullOrWhiteSpace(normalizedLanguageCode))
        {
            // Bible container publication modal shows both:
            // - publications in the selected language
            // - publications without a language FK (LanguageId == null)
            query = query.Where(pl =>
                (pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode) ||
                pl.LanguageId == null);
        }
        else
        {
            // If language isn't set yet, only count no-language publications for this category.
            query = query.Where(pl => pl.LanguageId == null);
        }

        var publicationCodes = await query
            .Select(pl => pl.PublicationCode)
            .ToListAsync();

        if (publicationCodes.Count == 0)
        {
            return 0;
        }

        // Deduplicate in-memory (including drama canonicalization).
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in publicationCodes)
        {
            var lower = code.ToLowerInvariant();
            if (PublicationTypeHelper.IsDrama(lower))
            {
                unique.Add(lower.Equals("dramas", StringComparison.OrdinalIgnoreCase) ? "Dramas" : "DramaticBibleReadings");
            }
            else
            {
                unique.Add(code);
            }
        }

        return unique.Count;
    }

    private static async Task<int?> GetBiblePublicationSectionModalItemCountAsync(MediaDbContext db, ScheduleStateItem schedule)
    {
        var publicationCode = schedule.BiblePublicationCode;
        if (string.IsNullOrWhiteSpace(publicationCode) || !PublicationTypeHelper.HasSectionStructure(publicationCode))
        {
            return 0;
        }

        var languageCode = schedule.BiblePublicationLanguageCode;
        var normalizedLanguageCode = string.IsNullOrWhiteSpace(languageCode) ? null : languageCode.ToUpperInvariant();

        var query = db.SectionLanguages
            .AsNoTracking()
            .Where(sl => sl.PublicationCode == publicationCode);

        if (!string.IsNullOrWhiteSpace(normalizedLanguageCode))
        {
            // Section modal shows both:
            // - sections in the selected language
            // - sections without a language FK (LanguageId == null)
            query = query.Where(sl =>
                (sl.Language != null && sl.Language.LanguageCode == normalizedLanguageCode) ||
                sl.LanguageId == null);
        }
        else
        {
            query = query.Where(sl => sl.LanguageId == null);
        }

        return await query
            .Select(sl => sl.SectionCode)
            .Distinct()
            .CountAsync();
    }

    /// <summary>
    /// Publication modal count for Music row. Category is always "Music" (harmony with Bible row which uses category from schedule).
    /// When MusicLanguageCode is null (melody), effective language "E" so modal shows E + no-language publications.
    /// </summary>
    private static async Task<int?> GetMusicPublicationModalItemCountAsync(MediaDbContext db, ScheduleStateItem schedule)
    {
        // When MusicLanguageCode is null (no explicit language selected), default to "E" (English)
        // for display and cascade purposes - this shows publications for "E" + no-language publications.
        var languageCode = schedule.MusicLanguageCode;
        var effectiveLanguageCode = string.IsNullOrEmpty(languageCode) ? "E" : languageCode;
        var normalizedLanguageCode = effectiveLanguageCode.ToUpperInvariant();
        
        var query = db.PublicationLanguages
            .AsNoTracking()
            .Where(pl => pl.Category != null && pl.Category.CategoryName == "Music");

        // Music publication modal shows both:
        // - publications in the selected language (or default "E" when null)
        // - publications without a language FK (LanguageId == null)
        query = query.Where(pl =>
            (pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode) ||
            pl.LanguageId == null);

        return await query
            .Select(pl => pl.PublicationCode)
            .Distinct()
            .CountAsync();
    }

    private static async Task<int?> GetMusicSectionModalItemCountAsync(MediaDbContext db, ScheduleStateItem schedule)
    {
        var publicationCode = schedule.MusicPublicationCode;
        if (string.IsNullOrWhiteSpace(publicationCode) || !PublicationTypeHelper.HasSectionStructure(publicationCode))
        {
            return 0;
        }

        // When MusicLanguageCode is null (no explicit language selected), default to "E" (English).
        // For sectioned publications like "iam", count sections for "E" + no-language sections.
        var languageCode = schedule.MusicLanguageCode;
        var effectiveLanguageCode = string.IsNullOrEmpty(languageCode) ? "E" : languageCode;
        var normalizedLanguageCode = effectiveLanguageCode.ToUpperInvariant();
        
        var query = db.SectionLanguages
            .AsNoTracking()
            .Where(sl => sl.PublicationCode == publicationCode);

        // Section modal shows both:
        // - sections in the selected language (or default "E" when null)
        // - sections without a language FK (LanguageId == null)
        query = query.Where(sl =>
            (sl.Language != null && sl.Language.LanguageCode == normalizedLanguageCode) ||
            sl.LanguageId == null);

        return await query
            .Select(sl => sl.SectionCode)
            .Distinct()
            .CountAsync();
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

    /// <summary>
    /// Populates MusicSectionName in the state item when we have a track number but no section name for instrumental music.
    /// This is used when switching music types where the track number is preserved but section name is missing.
    /// Follows the same pattern as Bible container: only populate section name if publication has section structure.
    /// Music type is inferred from LanguageCode: null/empty = melody (instrumental), otherwise = vocal.
    /// </summary>
    private async Task PopulateMusicSectionNameForStateAsync(ScheduleStateItem scheduleStateItem, IDispatcher dispatcher)
    {
        try
        {
            // Music type is inferred from LanguageCode: null/empty = melody (instrumental), otherwise = vocal
            var isMelodyMusic = string.IsNullOrEmpty(scheduleStateItem.MusicLanguageCode);
            if (!isMelodyMusic ||
                string.IsNullOrWhiteSpace(scheduleStateItem.MusicTrackCode) ||
                string.IsNullOrWhiteSpace(scheduleStateItem.MusicPublicationCode))
            {
                return;
            }

            // If section name is already populated, skip
            if (!string.IsNullOrWhiteSpace(scheduleStateItem.MusicSectionName))
            {
                return;
            }

            // Check if publication has section structure (following Bible container pattern)
            // Only populate section name if publication actually has sections
            if (!Shared.Helpers.PublicationTypeHelper.HasSectionStructure(scheduleStateItem.MusicPublicationCode))
            {
                // Non-sectioned publication - clear section code/name if they exist
                if (!string.IsNullOrWhiteSpace(scheduleStateItem.MusicSectionCode))
                {
                    var updatedSchedule = scheduleStateItem.DeepClone();
                    updatedSchedule.MusicSectionCode = null;
                    updatedSchedule.MusicSectionName = null;

                    Log.Debug("ScheduleEffects: Cleared MusicSectionCode and MusicSectionName for non-sectioned publication {PublicationCode}",
                        scheduleStateItem.MusicPublicationCode);

                    dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, musicUpdated: true, biblePublicationUpdated: false, shouldSave: false));
                }
                return;
            }

            var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>();
            if (scopeFactory == null)
            {
                Log.Warning("ScheduleEffects: ServiceProvider not available for music section lookup by track");
                return;
            }
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            // Find the section that contains this track
            var trackCode = scheduleStateItem.MusicTrackCode ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trackCode))
            {
                return;
            }

            var sectionInfo = await dbContext.BiblePublicationTracks
                .AsNoTracking()
                .Include(t => t.Section)
                    .ThenInclude(s => s!.BiblePublication)
                        .ThenInclude(p => p.Category)
                .Where(t => t.BiblePublicationSectionId != null
                    && t.TrackCode == trackCode
                    && t.Publication.PublicationCode == scheduleStateItem.MusicPublicationCode
                    && t.Publication.Category.CategoryName == "Music"
                    && t.Publication.LanguageId == null)
                .Select(t => new { t.Section!.SectionCode, t.Section.Name })
                .FirstOrDefaultAsync();

            if (sectionInfo != null && !string.IsNullOrWhiteSpace(sectionInfo.SectionCode))
            {
                // Update the state item with section code and name
                var updatedSchedule = scheduleStateItem.DeepClone();
                updatedSchedule.MusicSectionCode = sectionInfo.SectionCode;
                updatedSchedule.MusicSectionName = sectionInfo.Name;

                Log.Debug("ScheduleEffects: Populated MusicSectionCode '{MusicSectionCode}' and MusicSectionName '{MusicSectionName}' from track {TrackCode}",
                    sectionInfo.SectionCode, sectionInfo.Name, scheduleStateItem.MusicTrackCode);

                // Dispatch update to state (without saving to DB)
                dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, musicUpdated: true, biblePublicationUpdated: false, shouldSave: false));
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating music section name for state");
        }
    }
}

