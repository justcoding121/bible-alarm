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

    /// <summary>
    /// Effect: Transform DB entity to DTO and dispatch success action.
    /// Called when AddScheduleAction is dispatched with a DB entity.
    /// </summary>
    [EffectMethod]
    public async Task HandleAddSchedule(AddScheduleAction action, IDispatcher dispatcher)
    {
        try
        {
            Log.Information("ScheduleEffects: HandleAddSchedule - ScheduleId: {ScheduleId}, Name: {Name}",
                action.Schedule?.Id, action.Schedule?.Name);

            if (action.Schedule == null)
            {
                Log.Warning("ScheduleEffects: HandleAddSchedule - Schedule is null, skipping");
                return;
            }

            // Transform DB entity to State DTO (following Fluxor best practices)
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(action.Schedule);

            // Populate BibleReadingLanguageName, BibleReadingPublicationName, and BibleReadingBookName if BibleReadingSchedule exists
            await displayNamePopulator.PopulateTranslationNameAsync(scheduleStateItem, action.Schedule);
            await displayNamePopulator.PopulatePublicationNameAsync(scheduleStateItem, action.Schedule);
            await displayNamePopulator.PopulateBookNameAsync(scheduleStateItem, action.Schedule);

            // Populate music display properties if Music exists
            await displayNamePopulator.PopulateMusicLanguageNameAsync(scheduleStateItem, action.Schedule);
            await displayNamePopulator.PopulateMusicPublicationNameAsync(scheduleStateItem, action.Schedule);
            await displayNamePopulator.PopulateMusicTrackNameAsync(scheduleStateItem, action.Schedule);

            // Dispatch success action with DTO (reducer will handle this)
            dispatcher.Dispatch(new AddScheduleSuccessAction(scheduleStateItem));

            Log.Information("ScheduleEffects: HandleAddSchedule - Dispatched AddScheduleSuccessAction for ScheduleId: {ScheduleId}",
                scheduleStateItem.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error in HandleAddSchedule");
        }
    }

    /// <summary>
    /// Effect: Transform DB entity to DTO and dispatch success action.
    /// Called when UpdateScheduleAction is dispatched with a DB entity.
    /// </summary>
    [EffectMethod]
    public async Task HandleUpdateSchedule(UpdateScheduleAction action, IDispatcher dispatcher)
    {
        try
        {
            Log.Information("ScheduleEffects: HandleUpdateSchedule - ScheduleId: {ScheduleId}, Name: {Name}",
                action.Schedule?.Id, action.Schedule?.Name);

            if (action.Schedule == null)
            {
                Log.Warning("ScheduleEffects: HandleUpdateSchedule - Schedule is null, skipping");
                return;
            }

            // Clear cache BEFORE processing (schedule was already saved to DB in PlaylistService)
            // This prevents stale cache if process crashes after save but before refresh
            cacheManager.InvalidateScheduleCache();

            // Transform DB entity to State DTO
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(action.Schedule);

            // Populate BibleReadingLanguageName, BibleReadingPublicationName, and BibleReadingBookName if missing
            // (Note: This is for UpdateScheduleAction which doesn't go through the optimistic reducer)
            if (string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingLanguageName))
            {
                await displayNamePopulator.PopulateTranslationNameAsync(scheduleStateItem, action.Schedule);
            }
            if (string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingPublicationName))
            {
                await displayNamePopulator.PopulatePublicationNameAsync(scheduleStateItem, action.Schedule);
            }
            if (string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingBookName))
            {
                await displayNamePopulator.PopulateBookNameAsync(scheduleStateItem, action.Schedule);
            }

            // Populate music display properties if missing
            if (string.IsNullOrWhiteSpace(scheduleStateItem.MusicLanguageName))
            {
                await displayNamePopulator.PopulateMusicLanguageNameAsync(scheduleStateItem, action.Schedule);
            }
            if (string.IsNullOrWhiteSpace(scheduleStateItem.MusicPublicationName))
            {
                await displayNamePopulator.PopulateMusicPublicationNameAsync(scheduleStateItem, action.Schedule);
            }
            if (string.IsNullOrWhiteSpace(scheduleStateItem.MusicTrackName))
            {
                await displayNamePopulator.PopulateMusicTrackNameAsync(scheduleStateItem, action.Schedule);
            }

            // Dispatch success action with DTO (reducer will handle this)
            dispatcher.Dispatch(new UpdateScheduleSuccessAction(scheduleStateItem));

            Log.Information("ScheduleEffects: HandleUpdateSchedule - Dispatched UpdateScheduleSuccessAction for ScheduleId: {ScheduleId}",
                scheduleStateItem.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error in HandleUpdateSchedule");
        }
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
        try
        {
            Log.Information("ScheduleEffects: HandleCreateSchedule - Name: {Name}", action.Schedule?.Name);

            if (action.Schedule == null || alarmScheduleService == null)
            {
                Log.Warning("ScheduleEffects: HandleCreateSchedule - Schedule is null or service unavailable, skipping");
                if (action.Schedule != null)
                {
                    dispatcher.Dispatch(new CreateScheduleFailureAction(action.Schedule, "Service unavailable"));
                }
                return;
            }

            // Clear cache BEFORE save to prevent stale cache if process crashes
            cacheManager.InvalidateScheduleCache();

            // Map domain model (ScheduleStateItem) → DB entity (AlarmSchedule)
            var dbSchedule = mapper.Map<AlarmSchedule>(action.Schedule);

            Log.Debug("ScheduleEffects: HandleCreateSchedule - Before save. PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                dbSchedule.BibleReadingSchedule?.PublicationCode ?? "null",
                dbSchedule.BibleReadingSchedule?.LanguageCode ?? "null");

            // Set ID to 0 for new schedule (EF Core will generate it)
            dbSchedule.Id = 0;

            // Save to database
            var savedSchedule = await alarmScheduleService.AddScheduleAsync(dbSchedule, CancellationToken.None);

            Log.Debug("ScheduleEffects: HandleCreateSchedule - After save. PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                savedSchedule.BibleReadingSchedule?.PublicationCode ?? "null",
                savedSchedule.BibleReadingSchedule?.LanguageCode ?? "null");

            Log.Information("ScheduleEffects: HandleCreateSchedule - Saved to DB. ScheduleId: {ScheduleId}", savedSchedule.Id);

            // Create alarm if enabled
            if (savedSchedule.IsEnabled && alarmService != null)
            {
                await alarmService.Create(savedSchedule);
            }

            // Map DB entity → domain model (ScheduleStateItem)
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(savedSchedule);

            // IMPORTANT: Display names are already populated in action.Schedule (from CurrentSchedule state).
            // Selection pages/containers populate display names when user selects items (via HandleChapterSelected/HandleTrackSelected effects).
            // We should NOT query the database here - just preserve the display names from the action.
            // Copy display names from action.Schedule to scheduleStateItem (which was mapped from savedSchedule, so it doesn't have display names)
            if (action.Schedule != null)
            {
                scheduleStateItem.BibleReadingLanguageName = action.Schedule.BibleReadingLanguageName;
                scheduleStateItem.BibleReadingPublicationName = action.Schedule.BibleReadingPublicationName;
                scheduleStateItem.BibleReadingBookName = action.Schedule.BibleReadingBookName;
                scheduleStateItem.MusicLanguageName = action.Schedule.MusicLanguageName;
                scheduleStateItem.MusicPublicationName = action.Schedule.MusicPublicationName;
                scheduleStateItem.MusicTrackName = action.Schedule.MusicTrackName;
            }

            // Dispatch success action with DTO (display names preserved from state)
            dispatcher.Dispatch(new CreateScheduleSuccessAction(scheduleStateItem));

            Log.Information("ScheduleEffects: HandleCreateSchedule - Dispatched CreateScheduleSuccessAction for ScheduleId: {ScheduleId}",
                scheduleStateItem.Id);

            // Refresh cache in background after successful save
            _ = Task.Run(async () => await cacheManager.RefreshScheduleCacheAsync());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error in HandleCreateSchedule");
            if (action.Schedule != null)
            {
                dispatcher.Dispatch(new CreateScheduleFailureAction(action.Schedule, ex.Message));
            }
        }
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

    /// <summary>
    /// Effect: Handle UpdateScheduleSuccessAction - Invalidate cache when a schedule is updated in the database.
    /// This covers chapter navigation, enable/disable toggle, track changes, and other schedule updates.
    /// </summary>
    [EffectMethod]
    public Task HandleUpdateScheduleSuccess(UpdateScheduleSuccessAction action, IDispatcher dispatcher)
    {
        try
        {
            Log.Debug("ScheduleEffects: HandleUpdateScheduleSuccess - Schedule updated in DB, refreshing cache for schedule {ScheduleId}", action.Schedule?.Id);

            // Refresh cache in background after successful DB update
            _ = Task.Run(async () => await cacheManager.RefreshScheduleCacheAsync());

            // Dispatch SetCarPlayScreenAction to refresh Android Auto metadata
            // This will trigger DefaultCarScreenEffect to fetch metadata and update MediaSession
            // MediaSessionEffect will check if playback is active and skip if needed
            dispatcher.Dispatch(new SetCarPlayScreenAction());

            Log.Information("ScheduleEffects: HandleUpdateScheduleSuccess - Invalidated cache and dispatched SetCarPlayScreenAction for schedule {ScheduleId}", action.Schedule?.Id);

            // Note: OnLoadChildren is already being called by Android Auto in response to NotifyChildrenChanged
            // which is triggered when the change tracker detects a change in OnUpdateScheduleFromViewModel.
            // No additional force refresh is needed.
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error in HandleUpdateScheduleSuccess");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Effect: Handle RemoveScheduleSuccessAction - Invalidate cache when a schedule is deleted from the database.
    /// Also refreshes last played metadata if deleted schedule was the last played item and refreshes Android Auto.
    /// </summary>
    [EffectMethod]
    public async Task HandleRemoveScheduleSuccess(RemoveScheduleSuccessAction action, IDispatcher dispatcher)
    {
        try
        {
            Log.Debug("ScheduleEffects: HandleRemoveScheduleSuccess - Schedule deleted from DB, refreshing cache for schedule {ScheduleId}", action.ScheduleId);

            // Refresh cache in background after successful DB delete
            // Note: Cache was already cleared before delete, this just refreshes it
            _ = Task.Run(async () => await cacheManager.RefreshScheduleCacheAsync());

            // Check if the deleted schedule was the last played item saved in Preferences
            var lastPlayedMetadata = LastPlayedMetadataHelper.GetLastPlayedMetadata();
            if (lastPlayedMetadata.HasValue && lastPlayedMetadata.Value.ScheduleId == action.ScheduleId)
            {
                Log.Information("ScheduleEffects: HandleRemoveScheduleSuccess - Deleted schedule {ScheduleId} was the last played item, refreshing metadata", action.ScheduleId);

                // Get default schedule service to refresh metadata
                var defaultScheduleService = ServiceProviderManager.GetService<IDefaultScheduleService>();
                if (defaultScheduleService != null)
                {
                    // GetNextScheduleTrackMetaDataAsync will automatically save to Preferences
                    await defaultScheduleService.GetNextScheduleTrackMetaDataAsync();
                    Log.Information("ScheduleEffects: HandleRemoveScheduleSuccess - Refreshed last played metadata after schedule deletion");
                }
                else
                {
                    Log.Warning("ScheduleEffects: HandleRemoveScheduleSuccess - IDefaultScheduleService not available, cannot refresh metadata");
                }
            }
            else
            {
                Log.Debug("ScheduleEffects: HandleRemoveScheduleSuccess - Deleted schedule {ScheduleId} was not the last played item (LastPlayedScheduleId: {LastPlayedScheduleId}), no refresh needed",
                    action.ScheduleId, lastPlayedMetadata?.ScheduleId?.ToString() ?? "null");
            }

            // Always dispatch SetCarPlayScreenAction to refresh Android Auto metadata after deletion
            // This ensures Android Auto gets updated default schedule metadata, matching the update flow
            dispatcher.Dispatch(new SetCarPlayScreenAction());

            Log.Information("ScheduleEffects: HandleRemoveScheduleSuccess - Invalidated cache and dispatched SetCarPlayScreenAction for deleted schedule {ScheduleId}", action.ScheduleId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error in HandleRemoveScheduleSuccess");
        }
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

