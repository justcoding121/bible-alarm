#nullable enable
using AutoMapper;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
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
using System.Diagnostics.CodeAnalysis;
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
        scheduleDisplayNameService: scheduleDisplayNameService);
    private readonly TrackSelectionSyncHandler trackSyncHandler = new(state);

    // Effect handlers - initialized lazily when first accessed
    private ScheduleAddHandler? _addHandler;
    private ScheduleUpdateHandler? _updateHandler;
    private ScheduleCreateHandler? _createHandler;
    private ScheduleDeleteHandler? _deleteHandler;

    private ScheduleAddHandler addHandler => _addHandler ??= new ScheduleAddHandler(mapper, scheduleDisplayNameService);
    private ScheduleUpdateHandler updateHandler => _updateHandler ??= new ScheduleUpdateHandler(mapper, scheduleDisplayNameService);
    private ScheduleCreateHandler createHandler => _createHandler ??= new ScheduleCreateHandler(mapper, this.alarmScheduleService, this.alarmService, scheduleDisplayNameService);
    private ScheduleDeleteHandler deleteHandler => _deleteHandler ??= new ScheduleDeleteHandler(mapper, this.alarmScheduleService, this.alarmService, this.mediaCacheService, scheduleDisplayNameService);

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
            Log.Error(ex, AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ErrorInHandleViewScheduleModalCounts);
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
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Fluxor EffectMethod must remain instance.")]
    [EffectMethod]
    public Task HandleRemoveSchedule(RemoveScheduleAction action, IDispatcher dispatcher)
    {
        try
        {
            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleRemoveScheduleScheduleId,
                action.Schedule?.Id);

            if (action.Schedule == null)
            {
                Log.Warning(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleRemoveScheduleScheduleNullSkipping);
                return Task.CompletedTask;
            }

            // Dispatch success action with schedule ID (reducer will handle this)
            dispatcher.Dispatch(new RemoveScheduleSuccessAction(action.Schedule.Id));

            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleRemoveScheduleDispatchedRemoveScheduleSuccess,
                action.Schedule.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ErrorInHandleRemoveSchedule);
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
            ScheduleUpdateProcessor.LogUpdateStart(action);

            if (action.Schedule == null)
            {
                Log.Warning(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelScheduleNullSkipping);
                return;
            }

            if (!action.ShouldSave)
            {
                Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelShouldSaveFalseSkippingDb);
                
                // Even when not saving, we may need to populate missing display names (e.g., section name from track number)
                // This is especially important when switching music types where track number is preserved but section name is missing
                var isMelodyMusic = !string.IsNullOrWhiteSpace(action.Schedule.MusicPublicationCode)
                    && Bible.Alarm.Shared.Helpers.JwSourceHelper.MelodyMusicPublicationCodes.Contains(action.Schedule.MusicPublicationCode);
                if (action.MusicUpdated && isMelodyMusic &&
                    !string.IsNullOrWhiteSpace(action.Schedule.MusicTrackCode) &&
                    string.IsNullOrWhiteSpace(action.Schedule.MusicSectionName) &&
                    !string.IsNullOrWhiteSpace(action.Schedule.MusicPublicationCode))
                {
                    Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelPopulateMusicSectionNameInstrumental,
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
                ScheduleUpdateProcessor.HandleServiceUnavailable(action, dispatcher);
                return;
            }

            var savedSchedule = await updateProcessor.UpdateScheduleInDatabaseAsync(action);
            await updateProcessor.UpdateAlarmAsync(savedSchedule);

            var scheduleStateItem = await updateProcessor.MapAndPreserveDisplayNames(action, savedSchedule);
            dispatcher.Dispatch(new UpdateScheduleSuccessAction(scheduleStateItem));

            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateScheduleFromViewModelDispatchedUpdateScheduleSuccess,
                scheduleStateItem.Id, scheduleStateItem.MusicLanguageCode ?? "null");

            // Note: Cache invalidation is handled by HandleUpdateScheduleSuccess effect to avoid duplication
        }
        catch (Exception ex)
        {
            Log.Error(ex, AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ErrorInHandleUpdateScheduleFromViewModel);
            if (action.Schedule != null)
            {
                dispatcher.Dispatch(new UpdateScheduleFailureAction(action.Schedule, ex.Message));
            }
        }
    }

    /// <summary>
    /// When Bible publication changes (category/publication/language/section/track), populates display names and BiblePublicationIsMusic
    /// so the music container visibility updates without a full save.
    /// Uses UpdateDraftScheduleAction to only update CurrentSchedule (schedule page draft)
    /// without leaking unsaved changes to the home page Schedules collection.
    /// </summary>
    [EffectMethod]
    public async Task HandleUpdateScheduleFromViewModelPopulateBibleDisplayNames(UpdateScheduleFromViewModelAction action, IDispatcher dispatcher)
    {
        try
        {
            if (!action.BiblePublicationUpdated || action.ShouldSave)
            {
                return;
            }

            var currentState = state ?? ServiceProviderManager.GetService<IState<ApplicationState>>()!;
            var currentSchedule = currentState.Value.CurrentSchedule;
            if (currentSchedule == null || currentSchedule.Id != action.Schedule.Id)
            {
                return;
            }

            var scheduleCopy = currentSchedule.DeepClone();
            var alarmSchedule = mapper.Map<AlarmSchedule>(scheduleCopy);
            await scheduleDisplayNameService.PopulateDisplayNamesAsync(scheduleCopy, alarmSchedule);
            dispatcher.Dispatch(new UpdateDraftScheduleAction(scheduleCopy));
            Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.PopulatedBibleDisplayNamesIsMusicForScheduleId, scheduleCopy.BiblePublicationIsMusic, scheduleCopy.Id);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, AppConstants.Logging.ScheduleEffectsDiagnosticsLog.WarningErrorPopulatingBibleDisplayNamesAfterBiblePublicationUpdated);
        }
    }

    [EffectMethod]
    public async Task HandleUpdateScheduleFromViewModelPopulateModalCounts(UpdateScheduleFromViewModelAction action, IDispatcher dispatcher)
    {
        try
        {
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

            Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleUpdateSchedulePopulateModalCountsRefreshing,
                action.BiblePublicationUpdated, action.MusicUpdated, musicNeedsModalCounts);

            await TryDispatchModalCountsUpdateAsync(currentSchedule, dispatcher, reason: "UpdateScheduleFromViewModelAction");
        }
        catch (Exception ex)
        {
            Log.Error(ex, AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ErrorPopulatingModalCountsFromUpdateScheduleFromViewModelAction);
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
            Log.Information(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleDeleteScheduleEffectMethodCalled, action.ScheduleId);

            Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleDeleteScheduleCallingDeleteHandler, action.ScheduleId);
            await deleteHandler.HandleAsync(action, dispatcher);
            Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleDeleteScheduleDeleteHandlerCompleted, action.ScheduleId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"ScheduleEffects: HandleDeleteSchedule failed for ScheduleId: {action?.ScheduleId ?? -1}",
                ex);
        }
    }

    /// <summary>
    /// Effect: Handle UpdateScheduleSuccessAction - Invalidate cache when a schedule is updated in the database.
    /// This covers track navigation, enable/disable toggle, track changes, and other schedule updates.
    /// </summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Fluxor EffectMethod must remain instance.")]
    [EffectMethod]
    public Task HandleUpdateScheduleSuccess(UpdateScheduleSuccessAction action, IDispatcher dispatcher)
    {
        return ScheduleSuccessHandler.HandleUpdateScheduleSuccess(action, dispatcher);
    }

    /// <summary>
    /// Effect: Handle RemoveScheduleSuccessAction - Invalidate cache when a schedule is deleted from the database.
    /// Also refreshes last played metadata if deleted schedule was the last played item and refreshes Android Auto.
    /// </summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Fluxor EffectMethod must remain instance.")]
    [EffectMethod]
    public async Task HandleRemoveScheduleSuccess(RemoveScheduleSuccessAction action, IDispatcher dispatcher)
    {
        await ScheduleSuccessHandler.HandleRemoveScheduleSuccess(action, dispatcher);
    }

    /// <summary>
    /// Effect: After Bible track selection, populate display names (including BiblePublicationIsMusic) so the music container
    /// visibility updates when switching between music and non-music publications (e.g. ChildrenSongs → ChildrenMovies).
    /// Then refresh modal counts.
    /// Uses UpdateDraftScheduleAction to only update CurrentSchedule (schedule page draft)
    /// without leaking unsaved changes to the home page Schedules collection or triggering
    /// side effects like SetCarPlayScreenAction/preferences writes.
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

            var scheduleCopy = currentSchedule.DeepClone();
            var alarmSchedule = mapper.Map<AlarmSchedule>(scheduleCopy);
            await scheduleDisplayNameService.PopulateDisplayNamesAsync(scheduleCopy, alarmSchedule);
            dispatcher.Dispatch(new UpdateDraftScheduleAction(scheduleCopy));
            Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.PopulatedBibleDisplayNamesAfterTrackSelection,
                scheduleCopy.BiblePublicationIsMusic, scheduleCopy.BiblePublicationCode);

            await TryDispatchModalCountsUpdateAsync(scheduleCopy, dispatcher, reason: "Bible TrackSelectedAction");
        }
        catch (Exception ex)
        {
            Log.Error(ex, AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ErrorInHandleBiblePublicationTrackSelected);
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
            Log.Error(ex, AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ErrorInHandleMusicSectionSelectedModalCounts);
        }
    }

    private static async Task TryDispatchModalCountsUpdateAsync(ScheduleStateItem currentSchedule, IDispatcher dispatcher, string reason)
    {
        try
        {
            var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>();
            if (scopeFactory == null)
            {
                Log.Warning(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.CannotPopulateModalCountsScopeFactoryUnavailable, currentSchedule.Id);
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

            Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.UpdatingModalCounts,
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
            Log.Error(ex, AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ErrorUpdatingModalCountsReasonScheduleId,
                reason, currentSchedule.Id);
        }
    }

    /// <summary>
    /// Effect: Auto-populate schedule when category is selected.
    /// Finds first language and publication for the category, catalogs if needed, and populates schedule.
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
            var languageNameService = ServiceProviderManager.GetService<Bible.Alarm.Shared.Services.Media.Interfaces.ILanguageNameService>()!;
            var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>()!;

            var handler = new CategorySelectionAutoPopulateHandler(
                currentBiblePublicationService,
                currentMediaService,
                languageContentService,
                languageNameService,
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
            Log.Error(ex, AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ErrorHandlingCategorySelectionForCategory, action.CategoryName);
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
            Log.Error(ex, AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ErrorHandlingBiblePublicationCascade);
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
                Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleMusicCascadeSkippingMusicUpdatedFalse, action.Schedule?.Id ?? 0);
                return;
            }

            Log.Debug(AppConstants.Logging.ScheduleEffectsDiagnosticsLog.HandleMusicCascadeTriggered,
                action.Schedule?.Id ?? 0, action.Schedule?.MusicPublicationCode ?? "null");

            var currentState = state ?? ServiceProviderManager.GetService<IState<ApplicationState>>()!;
            var currentMediaService = mediaService ?? ServiceProviderManager.GetService<IMediaService>()!;
            var languageContentService = ServiceProviderManager.GetService<Bible.Alarm.Shared.Services.Media.Interfaces.ILanguageContentService>()!;
            var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>()!;
            
            var handler = new MusicCascadeHandler(
                currentMediaService,
                languageContentService,
                currentState,
                scopeFactory,
                Log.Logger);
            
            await handler.HandleAsync(dispatcher);
        }
        catch (Exception ex)
        {
            Log.Error(ex, AppConstants.Logging.ScheduleEffectsDiagnosticsLog.ErrorHandlingMusicCascade);
        }
    }

}

