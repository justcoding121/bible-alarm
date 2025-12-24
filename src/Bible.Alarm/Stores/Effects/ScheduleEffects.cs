#nullable enable
using AutoMapper;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;
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
    IState<ApplicationState>? state = null)
{
    private readonly IBibleTranslationService? bibleTranslationService = bibleTranslationService ?? ServiceProviderManager.GetService<IBibleTranslationService>();
    private readonly IBibleBookService? bibleBookService = bibleBookService ?? ServiceProviderManager.GetService<IBibleBookService>();
    private readonly IAlarmScheduleService? alarmScheduleService = alarmScheduleService ?? ServiceProviderManager.GetService<IAlarmScheduleService>();
    private readonly IAlarmService? alarmService = alarmService ?? ServiceProviderManager.GetService<IAlarmService>();
    private readonly IMediaCacheService? mediaCacheService = mediaCacheService ?? ServiceProviderManager.GetService<IMediaCacheService>();
    private readonly IMediaService? mediaService = mediaService ?? ServiceProviderManager.GetService<IMediaService>();
    private readonly IState<ApplicationState>? state = state ?? ServiceProviderManager.GetService<IState<ApplicationState>>();

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
            await PopulateTranslationNameAsync(scheduleStateItem, action.Schedule);
            await PopulatePublicationNameAsync(scheduleStateItem, action.Schedule);
            await PopulateBookNameAsync(scheduleStateItem, action.Schedule);

            // Populate music display properties if Music exists
            await PopulateMusicLanguageNameAsync(scheduleStateItem, action.Schedule);
            await PopulateMusicPublicationNameAsync(scheduleStateItem, action.Schedule);
            await PopulateMusicTrackNameAsync(scheduleStateItem, action.Schedule);

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

            // Transform DB entity to State DTO
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(action.Schedule);

            // Populate BibleReadingLanguageName, BibleReadingPublicationName, and BibleReadingBookName if missing
            // (Note: This is for UpdateScheduleAction which doesn't go through the optimistic reducer)
            if (string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingLanguageName))
            {
                await PopulateTranslationNameAsync(scheduleStateItem, action.Schedule);
            }
            if (string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingPublicationName))
            {
                await PopulatePublicationNameAsync(scheduleStateItem, action.Schedule);
            }
            if (string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingBookName))
            {
                await PopulateBookNameAsync(scheduleStateItem, action.Schedule);
            }

            // Populate music display properties if missing
            if (string.IsNullOrWhiteSpace(scheduleStateItem.MusicLanguageName))
            {
                await PopulateMusicLanguageNameAsync(scheduleStateItem, action.Schedule);
            }
            if (string.IsNullOrWhiteSpace(scheduleStateItem.MusicPublicationName))
            {
                await PopulateMusicPublicationNameAsync(scheduleStateItem, action.Schedule);
            }
            if (string.IsNullOrWhiteSpace(scheduleStateItem.MusicTrackName))
            {
                await PopulateMusicTrackNameAsync(scheduleStateItem, action.Schedule);
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
            LogUpdateStart(action);

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
                HandleServiceUnavailable(action, dispatcher);
                return;
            }

            var savedSchedule = await UpdateScheduleInDatabaseAsync(action);
            await UpdateAlarmAsync(savedSchedule);

            var scheduleStateItem = MapAndPreserveDisplayNames(action, savedSchedule);
            dispatcher.Dispatch(new UpdateScheduleSuccessAction(scheduleStateItem));

            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Dispatched UpdateScheduleSuccessAction for ScheduleId: {ScheduleId}, scheduleStateItem.MusicType={MusicType}",
                scheduleStateItem.Id, scheduleStateItem.MusicType?.ToString() ?? "null");
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

    private void LogUpdateStart(UpdateScheduleFromViewModelAction action)
    {
        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - ScheduleId: {ScheduleId}, Name: {Name}, ShouldSave: {ShouldSave}",
            action.Schedule?.Id, action.Schedule?.Name, action.ShouldSave);
    }

    private void HandleServiceUnavailable(UpdateScheduleFromViewModelAction action, IDispatcher dispatcher)
    {
        Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - Service unavailable, skipping");
        dispatcher.Dispatch(new UpdateScheduleFailureAction(action.Schedule!, "Service unavailable"));
    }

    private async Task<AlarmSchedule> UpdateScheduleInDatabaseAsync(UpdateScheduleFromViewModelAction action)
    {
        var dbSchedule = mapper.Map<AlarmSchedule>(action.Schedule!);
        var savedSchedule = await alarmScheduleService!.UpdateScheduleByIdAsync(
            action.Schedule.Id,
            existing => UpdateScheduleEntity(existing, dbSchedule, action),
            CancellationToken.None);

        LogScheduleUpdateResult(savedSchedule);
        return savedSchedule;
    }

    private void LogScheduleUpdateResult(AlarmSchedule savedSchedule)
    {
        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updated in DB. ScheduleId: {ScheduleId}, savedSchedule.Music={HasMusic}, savedSchedule.Music.MusicType={MusicType}, savedSchedule.Music.TrackNumber={TrackNumber}, savedSchedule.Music.PublicationCode={PublicationCode}, savedSchedule.Music.LanguageCode={LanguageCode}",
            savedSchedule.Id,
            savedSchedule.Music != null ? "not null" : "null",
            savedSchedule.Music?.MusicType.ToString() ?? "null",
            savedSchedule.Music?.TrackNumber.ToString() ?? "null",
            savedSchedule.Music?.PublicationCode ?? "null",
            savedSchedule.Music?.LanguageCode ?? "null");
    }

    private async Task UpdateAlarmAsync(AlarmSchedule savedSchedule)
    {
        if (alarmService != null)
        {
            await Task.Run(() => alarmService.Update(savedSchedule));
        }
    }

    private ScheduleStateItem MapAndPreserveDisplayNames(UpdateScheduleFromViewModelAction action, AlarmSchedule savedSchedule)
    {
        var scheduleStateItem = mapper.Map<ScheduleStateItem>(savedSchedule);
        LogMappingResult(scheduleStateItem);

        if (action.Schedule != null)
        {
            CopyDisplayNamesFromAction(scheduleStateItem, action.Schedule);
            PreserveMusicPropertiesIfNeeded(scheduleStateItem, action.Schedule);
        }

        return scheduleStateItem;
    }

    private void LogMappingResult(ScheduleStateItem scheduleStateItem)
    {
        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - After mapping savedSchedule to scheduleStateItem. scheduleStateItem.MusicType={MusicType}, scheduleStateItem.MusicTrackNumber={TrackNumber}, scheduleStateItem.MusicPublicationCode={PublicationCode}, scheduleStateItem.MusicLanguageCode={LanguageCode}",
            scheduleStateItem.MusicType?.ToString() ?? "null",
            scheduleStateItem.MusicTrackNumber?.ToString() ?? "null",
            scheduleStateItem.MusicPublicationCode ?? "null",
            scheduleStateItem.MusicLanguageCode ?? "null");
    }

    private void CopyDisplayNamesFromAction(ScheduleStateItem scheduleStateItem, ScheduleStateItem actionSchedule)
    {
        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Copying display names from action.Schedule. action.Schedule.MusicType={MusicType}, action.Schedule.MusicTrackNumber={TrackNumber}",
            actionSchedule.MusicType?.ToString() ?? "null",
            actionSchedule.MusicTrackNumber?.ToString() ?? "null");

        scheduleStateItem.BibleReadingLanguageName = actionSchedule.BibleReadingLanguageName;
        scheduleStateItem.BibleReadingPublicationName = actionSchedule.BibleReadingPublicationName;
        scheduleStateItem.BibleReadingBookName = actionSchedule.BibleReadingBookName;
        scheduleStateItem.MusicLanguageName = actionSchedule.MusicLanguageName;
        scheduleStateItem.MusicPublicationName = actionSchedule.MusicPublicationName;
        scheduleStateItem.MusicTrackName = actionSchedule.MusicTrackName;
    }

    private void PreserveMusicPropertiesIfNeeded(ScheduleStateItem scheduleStateItem, ScheduleStateItem actionSchedule)
    {
        if (actionSchedule.MusicType.HasValue &&
            (!scheduleStateItem.MusicType.HasValue || scheduleStateItem.MusicType.Value != actionSchedule.MusicType.Value))
        {
            Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - MusicType mismatch! savedSchedule.Music.MusicType={SavedMusicType}, action.Schedule.MusicType={ActionMusicType}. Using action.Schedule.MusicType.",
                scheduleStateItem.MusicType?.ToString() ?? "null",
                actionSchedule.MusicType?.ToString() ?? "null");

            scheduleStateItem.MusicType = actionSchedule.MusicType;
            scheduleStateItem.MusicTrackNumber = actionSchedule.MusicTrackNumber;
            scheduleStateItem.MusicPublicationCode = actionSchedule.MusicPublicationCode;
            scheduleStateItem.MusicLanguageCode = actionSchedule.MusicLanguageCode;
            scheduleStateItem.MusicRepeat = actionSchedule.MusicRepeat;
            scheduleStateItem.MusicId = actionSchedule.MusicId;
        }
    }

    private static void UpdateScheduleEntity(AlarmSchedule existing, AlarmSchedule dbSchedule, UpdateScheduleFromViewModelAction action)
    {
        UpdateBasicScheduleProperties(existing, dbSchedule);

        if (action.MusicUpdated)
        {
            UpdateMusicEntity(existing, dbSchedule, action);
        }
        else
        {
            Log.Debug("ScheduleEffects: HandleUpdateScheduleFromViewModel - action.MusicUpdated=false, skipping music update");
        }

        UpdateBibleReadingEntity(existing, dbSchedule, action);
    }

    private static void UpdateBasicScheduleProperties(AlarmSchedule existing, AlarmSchedule dbSchedule)
    {
        existing.Hour = dbSchedule.Hour;
        existing.Minute = dbSchedule.Minute;
        existing.Second = dbSchedule.Second;
        existing.DaysOfWeek = dbSchedule.DaysOfWeek;
        existing.IsEnabled = dbSchedule.IsEnabled;
        existing.MusicEnabled = dbSchedule.MusicEnabled;
        existing.NotificationEnabled = dbSchedule.NotificationEnabled;
        existing.AlwaysPlayFromStart = dbSchedule.AlwaysPlayFromStart;
        existing.NumberOfChaptersToRead = dbSchedule.NumberOfChaptersToRead;
        existing.Name = dbSchedule.Name;
        existing.SnoozeMinutes = dbSchedule.SnoozeMinutes;
    }

    private static void UpdateMusicEntity(AlarmSchedule existing, AlarmSchedule dbSchedule, UpdateScheduleFromViewModelAction action)
    {
        if (dbSchedule.Music != null)
        {
            UpdateMusicFromDbSchedule(existing, dbSchedule);
        }
        else if (HasValidMusicProperties(action.Schedule))
        {
            UpdateMusicFromActionSchedule(existing, action);
        }
        else
        {
            Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - action.MusicUpdated=true but dbSchedule.Music is null and action.Schedule has no valid music properties");
        }
    }

    private static bool HasValidMusicProperties(ScheduleStateItem? schedule)
    {
        return schedule != null &&
               schedule.MusicType.HasValue &&
               schedule.MusicTrackNumber.HasValue &&
               schedule.MusicTrackNumber.Value > 0;
    }

    private static void UpdateMusicFromDbSchedule(AlarmSchedule existing, AlarmSchedule dbSchedule)
    {
        if (dbSchedule.Music == null)
        {
            Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - dbSchedule.Music is null, skipping music update");
            return;
        }
        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updating music. dbSchedule.Music.MusicType={MusicType}, dbSchedule.Music.TrackNumber={TrackNumber}, dbSchedule.Music.PublicationCode={PublicationCode}, dbSchedule.Music.LanguageCode={LanguageCode}",
            dbSchedule.Music.MusicType, dbSchedule.Music.TrackNumber, dbSchedule.Music.PublicationCode, dbSchedule.Music.LanguageCode);

        if (existing.Music == null)
        {
            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Creating new Music entity");
            existing.Music = dbSchedule.Music;
            existing.Music.AlarmScheduleId = existing.Id;
        }
        else
        {
            var oldMusicType = existing.Music.MusicType;
            var oldTrackNumber = existing.Music.TrackNumber;
            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updating existing Music. Old MusicType={OldMusicType}, Old TrackNumber={OldTrackNumber}",
                oldMusicType, oldTrackNumber);

            existing.Music.Repeat = dbSchedule.Music.Repeat;
            existing.Music.LanguageCode = dbSchedule.Music.LanguageCode;
            existing.Music.MusicType = dbSchedule.Music.MusicType;
            existing.Music.PublicationCode = dbSchedule.Music.PublicationCode;
            existing.Music.TrackNumber = dbSchedule.Music.TrackNumber;

            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updated Music. New MusicType={NewMusicType}, New TrackNumber={NewTrackNumber}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                existing.Music.MusicType, existing.Music.TrackNumber, existing.Music.PublicationCode, existing.Music.LanguageCode);
        }
    }

    private static void UpdateMusicFromActionSchedule(AlarmSchedule existing, UpdateScheduleFromViewModelAction action)
    {
        Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - action.MusicUpdated=true but dbSchedule.Music is null. Creating Music from action.Schedule. MusicType={MusicType}, TrackNumber={TrackNumber}",
            action.Schedule!.MusicType, action.Schedule.MusicTrackNumber);

        if (existing.Music == null)
        {
            existing.Music = CreateMusicFromActionSchedule(action.Schedule, existing.Id);
            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Created new Music entity from action.Schedule");
        }
        else
        {
            UpdateExistingMusicFromActionSchedule(existing, action.Schedule);
        }
    }

    private static AlarmMusic CreateMusicFromActionSchedule(ScheduleStateItem schedule, int alarmScheduleId)
    {
        return new AlarmMusic
        {
            Id = schedule.MusicId ?? 0,
            MusicType = schedule.MusicType!.Value,
            PublicationCode = schedule.MusicPublicationCode ?? string.Empty,
            LanguageCode = schedule.MusicLanguageCode,
            TrackNumber = schedule.MusicTrackNumber!.Value,
            Repeat = schedule.MusicRepeat ?? false,
            AlarmScheduleId = alarmScheduleId
        };
    }

    private static void UpdateExistingMusicFromActionSchedule(AlarmSchedule existing, ScheduleStateItem schedule)
    {
        var oldMusicType = existing.Music!.MusicType;
        var oldTrackNumber = existing.Music.TrackNumber;
        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updating existing Music from action.Schedule. Old MusicType={OldMusicType}, Old TrackNumber={OldTrackNumber}",
            oldMusicType, oldTrackNumber);

        existing.Music.MusicType = schedule.MusicType!.Value;
        existing.Music.PublicationCode = schedule.MusicPublicationCode ?? string.Empty;
        existing.Music.LanguageCode = schedule.MusicLanguageCode;
        existing.Music.TrackNumber = schedule.MusicTrackNumber!.Value;
        existing.Music.Repeat = schedule.MusicRepeat ?? false;

        if (schedule.MusicId.HasValue)
        {
            existing.Music.Id = schedule.MusicId.Value;
        }

        Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updated Music from action.Schedule. New MusicType={NewMusicType}, New TrackNumber={NewTrackNumber}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
            existing.Music.MusicType, existing.Music.TrackNumber, existing.Music.PublicationCode, existing.Music.LanguageCode);
    }

    private static void UpdateBibleReadingEntity(AlarmSchedule existing, AlarmSchedule dbSchedule, UpdateScheduleFromViewModelAction action)
    {
        if (dbSchedule.BibleReadingSchedule == null)
        {
            return;
        }

        if (existing.BibleReadingSchedule == null)
        {
            existing.BibleReadingSchedule = dbSchedule.BibleReadingSchedule;
            existing.BibleReadingSchedule.AlarmScheduleId = existing.Id;
        }
        else
        {
            UpdateExistingBibleReadingSchedule(existing.BibleReadingSchedule, dbSchedule.BibleReadingSchedule, action);
        }
    }

    private static void UpdateExistingBibleReadingSchedule(
        BibleReadingSchedule existing,
        BibleReadingSchedule dbSchedule,
        UpdateScheduleFromViewModelAction action)
    {
        existing.BookNumber = dbSchedule.BookNumber;
        existing.ChapterNumber = dbSchedule.ChapterNumber;
        existing.LanguageCode = dbSchedule.LanguageCode;
        existing.PublicationCode = dbSchedule.PublicationCode;

        if (action.BibleReadingUpdated)
        {
            existing.FinishedDuration = TimeSpan.Zero;
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
                        await PopulateTranslationNameAsync(scheduleToRestore, scheduleFromDb);
                        await PopulateBookNameAsync(scheduleToRestore, scheduleFromDb);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "ScheduleEffects: HandleDeleteSchedule - Failed to load schedule for rollback, ScheduleId: {ScheduleId}", action.ScheduleId);
                }

                dispatcher.Dispatch(new DeleteScheduleFailureAction(action.ScheduleId, "Cannot delete last schedule", scheduleToRestore));
                return;
            }

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
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error in HandleDeleteSchedule");
            dispatcher.Dispatch(new DeleteScheduleFailureAction(action.ScheduleId, ex.Message));
        }
    }

    /// <summary>
    /// Populate BibleReadingLanguageName from language dictionary if BibleReadingSchedule exists.
    /// </summary>
    private async Task PopulateTranslationNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.BibleReadingSchedule == null || bibleTranslationService == null)
        {
            return;
        }

        try
        {
            var languageCode = schedule.BibleReadingSchedule.LanguageCode;
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return;
            }

            var languagesDict = await bibleTranslationService.GetDistinctLanguagesAsync();
            if (languagesDict.TryGetValue(languageCode, out var language))
            {
                scheduleStateItem.BibleReadingLanguageName = language.Name;
                Log.Debug("ScheduleEffects: Set BibleReadingLanguageName '{BibleReadingLanguageName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    language.Name, schedule.Id, languageCode);
            }
            else
            {
                scheduleStateItem.BibleReadingLanguageName = languageCode;
                Log.Debug("ScheduleEffects: Language not found for LanguageCode '{LanguageCode}', using code as BibleReadingLanguageName for schedule {ScheduleId}",
                    languageCode, schedule.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating BibleReadingLanguageName for schedule {ScheduleId}", schedule.Id);
            // Fallback to language code
            scheduleStateItem.BibleReadingLanguageName = schedule.BibleReadingSchedule.LanguageCode;
        }
    }

    /// <summary>
    /// Populate BibleReadingLanguageName from language dictionary using language code from ScheduleStateItem.
    /// </summary>
    private async Task PopulateTranslationNameAsync(ScheduleStateItem scheduleStateItem)
    {
        if (string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingLanguageCode) || bibleTranslationService == null)
        {
            return;
        }

        try
        {
            var languagesDict = await bibleTranslationService.GetDistinctLanguagesAsync();
            if (languagesDict.TryGetValue(scheduleStateItem.BibleReadingLanguageCode, out var language))
            {
                scheduleStateItem.BibleReadingLanguageName = language.Name;
                Log.Debug("ScheduleEffects: Set BibleReadingLanguageName '{BibleReadingLanguageName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    language.Name, scheduleStateItem.Id, scheduleStateItem.BibleReadingLanguageCode);
            }
            else
            {
                scheduleStateItem.BibleReadingLanguageName = scheduleStateItem.BibleReadingLanguageCode;
                Log.Debug("ScheduleEffects: Language not found for LanguageCode '{LanguageCode}', using code as BibleReadingLanguageName for schedule {ScheduleId}",
                    scheduleStateItem.BibleReadingLanguageCode, scheduleStateItem.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating BibleReadingLanguageName for schedule {ScheduleId}", scheduleStateItem.Id);
            // Fallback to language code
            scheduleStateItem.BibleReadingLanguageName = scheduleStateItem.BibleReadingLanguageCode;
        }
    }

    /// <summary>
    /// Populate BibleReadingPublicationName from BibleTranslationService if BibleReadingSchedule exists.
    /// </summary>
    private async Task PopulatePublicationNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.BibleReadingSchedule == null || bibleTranslationService == null)
        {
            return;
        }

        try
        {
            var bibleReading = schedule.BibleReadingSchedule;
            if (string.IsNullOrWhiteSpace(bibleReading.LanguageCode) ||
                string.IsNullOrWhiteSpace(bibleReading.PublicationCode))
            {
                return;
            }

            var translation = await bibleTranslationService.GetByLanguageAndCodeWithBooksAsync(
                bibleReading.LanguageCode,
                bibleReading.PublicationCode);

            if (translation != null && !string.IsNullOrWhiteSpace(translation.Name))
            {
                scheduleStateItem.BibleReadingPublicationName = translation.Name;
                Log.Debug("ScheduleEffects: Set BibleReadingPublicationName '{BibleReadingPublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                    translation.Name, schedule.Id, bibleReading.PublicationCode);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating BibleReadingPublicationName for schedule {ScheduleId}", schedule.Id);
        }
    }

    /// <summary>
    /// Populate BookName from BibleBookService if BibleReadingSchedule exists.
    /// </summary>
    private async Task PopulateBookNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.BibleReadingSchedule == null || bibleBookService == null)
        {
            return;
        }

        try
        {
            var bibleReading = schedule.BibleReadingSchedule;
            if (bibleReading.BookNumber <= 0 ||
                string.IsNullOrWhiteSpace(bibleReading.LanguageCode) ||
                string.IsNullOrWhiteSpace(bibleReading.PublicationCode))
            {
                return;
            }

            var bookName = await bibleBookService.GetBookNameAsync(
                bibleReading.LanguageCode,
                bibleReading.PublicationCode,
                bibleReading.BookNumber);

            if (!string.IsNullOrWhiteSpace(bookName))
            {
                scheduleStateItem.BibleReadingBookName = bookName;
                Log.Debug("ScheduleEffects: Set BibleReadingBookName '{BibleReadingBookName}' for schedule {ScheduleId} (BookNumber: {BookNumber})",
                    bookName, schedule.Id, bibleReading.BookNumber);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating BookName for schedule {ScheduleId}", schedule.Id);
        }
    }

    /// <summary>
    /// Populate MusicLanguageName from vocal music languages if Music exists and is Vocals.
    /// </summary>
    private async Task PopulateMusicLanguageNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.Music == null || mediaService == null)
        {
            return;
        }

        try
        {
            var music = schedule.Music;
            // Only populate for vocals (melodies don't have language)
            if (music.MusicType != Shared.Models.Enums.MusicType.Vocals ||
                string.IsNullOrWhiteSpace(music.LanguageCode))
            {
                return;
            }

            var languagesDict = await mediaService.GetVocalMusicLanguages();
            if (languagesDict.TryGetValue(music.LanguageCode, out var language))
            {
                scheduleStateItem.MusicLanguageName = language.Name;
                Log.Debug("ScheduleEffects: Set MusicLanguageName '{MusicLanguageName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    language.Name, schedule.Id, music.LanguageCode);
            }
            else
            {
                scheduleStateItem.MusicLanguageName = music.LanguageCode;
                Log.Debug("ScheduleEffects: Language not found for LanguageCode '{LanguageCode}', using code as MusicLanguageName for schedule {ScheduleId}",
                    music.LanguageCode, schedule.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating MusicLanguageName for schedule {ScheduleId}", schedule.Id);
            if (schedule.Music != null)
            {
                scheduleStateItem.MusicLanguageName = schedule.Music.LanguageCode;
            }
        }
    }

    /// <summary>
    /// Populate MusicPublicationName from vocal music releases if Music exists and is Vocals.
    /// </summary>
    private async Task PopulateMusicPublicationNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.Music == null || mediaService == null)
        {
            return;
        }

        try
        {
            var music = schedule.Music;
            // Only populate for vocals (melodies don't have publication name in the same way)
            if (music.MusicType != Shared.Models.Enums.MusicType.Vocals ||
                string.IsNullOrWhiteSpace(music.LanguageCode) ||
                string.IsNullOrWhiteSpace(music.PublicationCode))
            {
                return;
            }

            var releases = await mediaService.GetVocalMusicReleases(music.LanguageCode);
            if (releases.TryGetValue(music.PublicationCode, out var release))
            {
                scheduleStateItem.MusicPublicationName = release.Name;
                Log.Debug("ScheduleEffects: Set MusicPublicationName '{MusicPublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                    release.Name, schedule.Id, music.PublicationCode);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating MusicPublicationName for schedule {ScheduleId}", schedule.Id);
        }
    }

    /// <summary>
    /// Populate MusicTrackName from music tracks if Music exists.
    /// </summary>
    private async Task PopulateMusicTrackNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.Music == null || mediaService == null)
        {
            return;
        }

        try
        {
            var music = schedule.Music;
            if (music.TrackNumber <= 0)
            {
                return;
            }

            string? trackName = null;
            if (music.MusicType == Shared.Models.Enums.MusicType.Melodies)
            {
                if (string.IsNullOrWhiteSpace(music.PublicationCode))
                {
                    return;
                }

                var tracks = await mediaService.GetMelodyMusicTracks(music.PublicationCode);
                if (tracks.TryGetValue(music.TrackNumber, out var track))
                {
                    // Format melody track title with prefix to match track modal display
                    trackName = $"Melody Number(s) {track.Title}";
                }
            }
            else if (music.MusicType == Shared.Models.Enums.MusicType.Vocals)
            {
                if (string.IsNullOrWhiteSpace(music.LanguageCode) || string.IsNullOrWhiteSpace(music.PublicationCode))
                {
                    return;
                }

                var tracks = await mediaService.GetVocalMusicTracks(music.LanguageCode, music.PublicationCode);
                if (tracks.TryGetValue(music.TrackNumber, out var track))
                {
                    trackName = track.Title;
                }
            }

            if (!string.IsNullOrWhiteSpace(trackName))
            {
                scheduleStateItem.MusicTrackName = trackName;
                Log.Debug("ScheduleEffects: Set MusicTrackName '{MusicTrackName}' for schedule {ScheduleId} (TrackNumber: {TrackNumber})",
                    trackName, schedule.Id, music.TrackNumber);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating MusicTrackName for schedule {ScheduleId}", schedule.Id);
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
        try
        {
            LogChapterSelectedStart(action);

            var currentState = state?.Value;
            if (!CanSyncChapterSelection(currentState, action))
            {
                return;
            }

            var currentSchedule = currentState!.CurrentSchedule!;
            if (!ShouldSyncBibleReadingSchedule(currentSchedule, action.CurrentBibleReadingSchedule!))
            {
                return;
            }

            var updatedSchedule = CreateUpdatedScheduleFromChapterSelection(currentSchedule, action);
            DispatchUpdateAction(dispatcher, updatedSchedule, currentSchedule.Id);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error syncing CurrentBibleReadingSchedule to CurrentSchedule");
        }
    }

    private void LogChapterSelectedStart(ChapterSelectedAction action)
    {
        Log.Information("ScheduleEffects: HandleChapterSelected - Received action. CurrentBibleReadingSchedule: {BibleReadingSchedule}, LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, BookNumber: {BookNumber}, ChapterNumber: {ChapterNumber}",
            action.CurrentBibleReadingSchedule != null ? "not null" : "null",
            action.CurrentBibleReadingSchedule?.LanguageCode ?? "null",
            action.CurrentBibleReadingSchedule?.PublicationCode ?? "null",
            action.CurrentBibleReadingSchedule?.BookNumber ?? 0,
            action.CurrentBibleReadingSchedule?.ChapterNumber ?? 0);
    }

    private bool CanSyncChapterSelection(ApplicationState? currentState, ChapterSelectedAction action)
    {
        if (currentState?.CurrentSchedule == null || action.CurrentBibleReadingSchedule == null)
        {
            Log.Warning("ScheduleEffects: HandleChapterSelected - CurrentSchedule or CurrentBibleReadingSchedule is null. CurrentSchedule: {CurrentSchedule}, CurrentBibleReadingSchedule: {BibleReadingSchedule}",
                currentState?.CurrentSchedule != null ? "not null" : "null",
                action.CurrentBibleReadingSchedule != null ? "not null" : "null");
            return false;
        }
        return true;
    }

    private bool ShouldSyncBibleReadingSchedule(ScheduleStateItem currentSchedule, BibleReadingStateItem actionBibleReadingSchedule)
    {
        Log.Debug("ScheduleEffects: HandleChapterSelected - CurrentSchedule Id: {ScheduleId}, BibleReadingScheduleId: {BibleReadingScheduleId}, Action BibleReadingSchedule Id: {ActionBibleReadingScheduleId}",
            currentSchedule.Id, currentSchedule.BibleReadingScheduleId, actionBibleReadingSchedule.Id);

        // Allow syncing if:
        // 1. Action has Id=0 (new selection, not yet saved) - always sync to update current schedule
        // 2. Action Id matches current schedule's BibleReadingScheduleId - same schedule, sync
        // Reject only if action has a non-zero ID that doesn't match (different schedule)
        if (actionBibleReadingSchedule.Id > 0 &&
            currentSchedule.BibleReadingScheduleId.HasValue &&
            actionBibleReadingSchedule.Id != currentSchedule.BibleReadingScheduleId.Value)
        {
            Log.Warning("ScheduleEffects: HandleChapterSelected - Different BibleReadingSchedule ID. Current: {CurrentId}, Action: {ActionId}. Not syncing.",
                currentSchedule.BibleReadingScheduleId.Value, actionBibleReadingSchedule.Id);
            return false;
        }

        Log.Debug("ScheduleEffects: HandleChapterSelected - Syncing allowed. Action Id: {ActionId} (0=new selection), Current BibleReadingScheduleId: {CurrentId}",
            actionBibleReadingSchedule.Id, currentSchedule.BibleReadingScheduleId);
        return true;
    }

    private ScheduleStateItem CreateUpdatedScheduleFromChapterSelection(ScheduleStateItem currentSchedule, ChapterSelectedAction action)
    {
        var updatedSchedule = CloneBasicScheduleProperties(currentSchedule);
        UpdateBibleReadingProperties(updatedSchedule, currentSchedule, action.CurrentBibleReadingSchedule!);
        PreserveMusicProperties(updatedSchedule, currentSchedule);
        SetBibleReadingDisplayNames(updatedSchedule, action.CurrentBibleReadingSchedule!);
        return updatedSchedule;
    }

    private static ScheduleStateItem CloneBasicScheduleProperties(ScheduleStateItem currentSchedule)
    {
        return new ScheduleStateItem
        {
            Id = currentSchedule.Id,
            Name = currentSchedule.Name,
            IsEnabled = currentSchedule.IsEnabled,
            Hour = currentSchedule.Hour,
            Minute = currentSchedule.Minute,
            Second = currentSchedule.Second,
            DaysOfWeek = currentSchedule.DaysOfWeek,
            NotificationEnabled = currentSchedule.NotificationEnabled,
            MusicEnabled = currentSchedule.MusicEnabled,
            SnoozeMinutes = currentSchedule.SnoozeMinutes,
            NumberOfChaptersToRead = currentSchedule.NumberOfChaptersToRead,
            AlwaysPlayFromStart = currentSchedule.AlwaysPlayFromStart,
            CurrentPlayItem = currentSchedule.CurrentPlayItem,
            LatestAlarmNotificationId = currentSchedule.LatestAlarmNotificationId
        };
    }

    private static void UpdateBibleReadingProperties(ScheduleStateItem updatedSchedule, ScheduleStateItem currentSchedule, BibleReadingStateItem actionBibleReadingSchedule)
    {
        updatedSchedule.BibleReadingScheduleId = actionBibleReadingSchedule.Id > 0 ? actionBibleReadingSchedule.Id : currentSchedule.BibleReadingScheduleId;
        updatedSchedule.BibleReadingLanguageCode = actionBibleReadingSchedule.LanguageCode;
        updatedSchedule.BibleReadingPublicationCode = actionBibleReadingSchedule.PublicationCode;
        updatedSchedule.BibleReadingBookNumber = actionBibleReadingSchedule.BookNumber;
        updatedSchedule.BibleReadingChapterNumber = actionBibleReadingSchedule.ChapterNumber;
        updatedSchedule.BibleReadingFinishedDuration = actionBibleReadingSchedule.FinishedDuration;
    }

    private static void PreserveMusicProperties(ScheduleStateItem updatedSchedule, ScheduleStateItem currentSchedule)
    {
        updatedSchedule.MusicId = currentSchedule.MusicId;
        updatedSchedule.MusicType = currentSchedule.MusicType;
        updatedSchedule.MusicPublicationCode = currentSchedule.MusicPublicationCode;
        updatedSchedule.MusicLanguageCode = currentSchedule.MusicLanguageCode;
        updatedSchedule.MusicTrackNumber = currentSchedule.MusicTrackNumber;
        updatedSchedule.MusicRepeat = currentSchedule.MusicRepeat;
        updatedSchedule.MusicLanguageName = currentSchedule.MusicLanguageName;
        updatedSchedule.MusicPublicationName = currentSchedule.MusicPublicationName;
        updatedSchedule.MusicTrackName = currentSchedule.MusicTrackName;
    }

    private void SetBibleReadingDisplayNames(ScheduleStateItem updatedSchedule, BibleReadingStateItem actionBibleReadingSchedule)
    {
        // IMPORTANT: Use display names from the action (populated from list items when user tapped).
        // Do NOT query the database - display names are already available from the selection.
        updatedSchedule.BibleReadingLanguageName = actionBibleReadingSchedule.LanguageName;
        updatedSchedule.BibleReadingPublicationName = actionBibleReadingSchedule.PublicationName;
        updatedSchedule.BibleReadingBookName = actionBibleReadingSchedule.BookName;

        Log.Debug("ScheduleEffects: HandleChapterSelected - Using display names from action. LanguageName: {LanguageName}, PublicationName: {PublicationName}, BookName: {BookName}",
            updatedSchedule.BibleReadingLanguageName ?? "null",
            updatedSchedule.BibleReadingPublicationName ?? "null",
            updatedSchedule.BibleReadingBookName ?? "null");
    }

    private void DispatchUpdateAction(IDispatcher dispatcher, ScheduleStateItem updatedSchedule, int scheduleId)
    {
        Log.Information("ScheduleEffects: HandleChapterSelected - Dispatching UpdateScheduleFromViewModelAction. ScheduleId: {ScheduleId}, LanguageCode: {LanguageCode}, LanguageName: {LanguageName}, PublicationCode: {PublicationCode}, PublicationName: {PublicationName}, BookNumber: {BookNumber}, BookName: {BookName}, ChapterNumber: {ChapterNumber}",
            updatedSchedule.Id,
            updatedSchedule.BibleReadingLanguageCode,
            updatedSchedule.BibleReadingLanguageName ?? "null",
            updatedSchedule.BibleReadingPublicationCode,
            updatedSchedule.BibleReadingPublicationName ?? "null",
            updatedSchedule.BibleReadingBookNumber,
            updatedSchedule.BibleReadingBookName ?? "null",
            updatedSchedule.BibleReadingChapterNumber);
        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, true, shouldSave: false));

        Log.Debug("ScheduleEffects: HandleChapterSelected - Synced CurrentBibleReadingSchedule to CurrentSchedule for ScheduleId: {ScheduleId}",
            scheduleId);
    }

    /// <summary>
    /// Effect: Sync CurrentMusic to CurrentSchedule when TrackSelectedAction is dispatched.
    /// This ensures that when sub-pages update CurrentMusic, CurrentSchedule is also updated
    /// so the schedule page displays the changes immediately.
    /// </summary>
    [EffectMethod]
    public async Task HandleTrackSelected(TrackSelectedAction action, IDispatcher dispatcher)
    {
        try
        {
            LogTrackSelectedStart(action);

            var currentState = state?.Value;
            if (!CanSyncTrackSelection(currentState, action))
            {
                return;
            }

            var currentSchedule = currentState!.CurrentSchedule!;
            var musicTypeChanged = HasMusicTypeChanged(currentSchedule, action.CurrentMusic!);

            if (!ShouldSyncMusic(currentSchedule, action.CurrentMusic!, musicTypeChanged))
            {
                return;
            }

            if (musicTypeChanged)
            {
                Log.Information("ScheduleEffects: HandleTrackSelected - Music type changed from {OldType} to {NewType}. Syncing.",
                    currentSchedule.MusicType, action.CurrentMusic.MusicType);
            }

            var updatedSchedule = CreateUpdatedScheduleFromTrackSelection(currentSchedule, action, musicTypeChanged);
            DispatchTrackUpdateAction(dispatcher, updatedSchedule, currentSchedule.Id);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error syncing CurrentMusic to CurrentSchedule");
        }
    }

    private void LogTrackSelectedStart(TrackSelectedAction action)
    {
        Log.Information("ScheduleEffects: HandleTrackSelected - Received action. CurrentMusic: {CurrentMusic}, MusicType: {MusicType}, PublicationCode: {PublicationCode}, TrackNumber: {TrackNumber}",
            action.CurrentMusic != null ? "not null" : "null",
            action.CurrentMusic?.MusicType ?? MusicType.Melodies,
            action.CurrentMusic?.PublicationCode ?? "null",
            action.CurrentMusic?.TrackNumber ?? 0);
    }

    private bool CanSyncTrackSelection(ApplicationState? currentState, TrackSelectedAction action)
    {
        if (currentState?.CurrentSchedule == null || action.CurrentMusic == null)
        {
            Log.Warning("ScheduleEffects: HandleTrackSelected - CurrentSchedule or CurrentMusic is null. CurrentSchedule: {CurrentSchedule}, CurrentMusic: {CurrentMusic}",
                currentState?.CurrentSchedule != null ? "not null" : "null",
                action.CurrentMusic != null ? "not null" : "null");
            return false;
        }
        return true;
    }

    private static bool HasMusicTypeChanged(ScheduleStateItem currentSchedule, MusicStateItem actionMusic)
    {
        return currentSchedule.MusicType.HasValue &&
               currentSchedule.MusicType.Value != actionMusic.MusicType;
    }

    private bool ShouldSyncMusic(ScheduleStateItem currentSchedule, MusicStateItem actionMusic, bool musicTypeChanged)
    {
        Log.Debug("ScheduleEffects: HandleTrackSelected - CurrentSchedule Id: {ScheduleId}, MusicId: {MusicId}, Action Music Id: {ActionMusicId}",
            currentSchedule.Id, currentSchedule.MusicId, actionMusic.Id);

        // Allow syncing if:
        // 1. Action has Id=0 (new selection, not yet saved) - always sync to update current schedule
        // 2. Action Id matches current schedule's MusicId - same schedule, sync
        // 3. Music type changed (e.g., Melodies -> Vocals) - always sync to update current schedule
        // Reject only if action has a non-zero ID that doesn't match (different schedule) AND music type hasn't changed
        if (actionMusic.Id > 0 &&
            currentSchedule.MusicId.HasValue &&
            actionMusic.Id != currentSchedule.MusicId.Value &&
            !musicTypeChanged)
        {
            Log.Warning("ScheduleEffects: HandleTrackSelected - Different Music ID. Current: {CurrentId}, Action: {ActionId}. Not syncing.",
                currentSchedule.MusicId.Value, actionMusic.Id);
            return false;
        }

        Log.Debug("ScheduleEffects: HandleTrackSelected - Syncing allowed. Action Id: {ActionId} (0=new selection), Current MusicId: {CurrentId}",
            actionMusic.Id, currentSchedule.MusicId);
        return true;
    }

    private ScheduleStateItem CreateUpdatedScheduleFromTrackSelection(ScheduleStateItem currentSchedule, TrackSelectedAction action, bool musicTypeChanged)
    {
        var updatedSchedule = CloneBasicScheduleProperties(currentSchedule);
        PreserveBibleReadingProperties(updatedSchedule, currentSchedule);
        UpdateMusicProperties(updatedSchedule, currentSchedule, action.CurrentMusic!, musicTypeChanged);
        SetMusicDisplayNames(updatedSchedule, action.CurrentMusic!);
        return updatedSchedule;
    }

    private static void PreserveBibleReadingProperties(ScheduleStateItem updatedSchedule, ScheduleStateItem currentSchedule)
    {
        updatedSchedule.BibleReadingScheduleId = currentSchedule.BibleReadingScheduleId;
        updatedSchedule.BibleReadingLanguageCode = currentSchedule.BibleReadingLanguageCode;
        updatedSchedule.BibleReadingPublicationCode = currentSchedule.BibleReadingPublicationCode;
        updatedSchedule.BibleReadingBookNumber = currentSchedule.BibleReadingBookNumber;
        updatedSchedule.BibleReadingChapterNumber = currentSchedule.BibleReadingChapterNumber;
        updatedSchedule.BibleReadingFinishedDuration = currentSchedule.BibleReadingFinishedDuration;
        updatedSchedule.BibleReadingLanguageName = currentSchedule.BibleReadingLanguageName;
        updatedSchedule.BibleReadingPublicationName = currentSchedule.BibleReadingPublicationName;
        updatedSchedule.BibleReadingBookName = currentSchedule.BibleReadingBookName;
    }

    private static void UpdateMusicProperties(ScheduleStateItem updatedSchedule, ScheduleStateItem currentSchedule, MusicStateItem actionMusic, bool musicTypeChanged)
    {
        // If music type changed or Id is 0 (new selection), set MusicId to null or action's Id
        // Otherwise preserve the existing MusicId
        updatedSchedule.MusicId = (musicTypeChanged || actionMusic.Id == 0)
            ? (actionMusic.Id > 0 ? actionMusic.Id : null)
            : currentSchedule.MusicId;
        updatedSchedule.MusicType = actionMusic.MusicType;
        updatedSchedule.MusicPublicationCode = actionMusic.PublicationCode;
        updatedSchedule.MusicLanguageCode = actionMusic.LanguageCode;
        updatedSchedule.MusicTrackNumber = actionMusic.TrackNumber;
        updatedSchedule.MusicRepeat = actionMusic.Repeat;
        // Preserve music display names from current schedule (will be repopulated if needed)
        updatedSchedule.MusicLanguageName = currentSchedule.MusicLanguageName;
        updatedSchedule.MusicPublicationName = currentSchedule.MusicPublicationName;
        updatedSchedule.MusicTrackName = currentSchedule.MusicTrackName;
    }

    private void SetMusicDisplayNames(ScheduleStateItem updatedSchedule, MusicStateItem actionMusic)
    {
        // IMPORTANT: Use display names from the action (populated from list items when user tapped).
        // Do NOT query the database - display names are already available from the selection.
        updatedSchedule.MusicLanguageName = actionMusic.LanguageName;
        updatedSchedule.MusicPublicationName = actionMusic.PublicationName;
        updatedSchedule.MusicTrackName = actionMusic.TrackName;

        Log.Debug("ScheduleEffects: HandleTrackSelected - Using display names from action. LanguageName: {LanguageName}, PublicationName: {PublicationName}, TrackName: {TrackName}",
            updatedSchedule.MusicLanguageName ?? "null",
            updatedSchedule.MusicPublicationName ?? "null",
            updatedSchedule.MusicTrackName ?? "null");
    }

    private void DispatchTrackUpdateAction(IDispatcher dispatcher, ScheduleStateItem updatedSchedule, int scheduleId)
    {
        Log.Information("ScheduleEffects: HandleTrackSelected - Dispatching UpdateScheduleFromViewModelAction. ScheduleId: {ScheduleId}, MusicType: {MusicType}, LanguageCode: {LanguageCode}, LanguageName: {LanguageName}, PublicationCode: {PublicationCode}, PublicationName: {PublicationName}, TrackNumber: {TrackNumber}, TrackName: {TrackName}",
            updatedSchedule.Id,
            updatedSchedule.MusicType,
            updatedSchedule.MusicLanguageCode ?? "null",
            updatedSchedule.MusicLanguageName ?? "null",
            updatedSchedule.MusicPublicationCode ?? "null",
            updatedSchedule.MusicPublicationName ?? "null",
            updatedSchedule.MusicTrackNumber,
            updatedSchedule.MusicTrackName ?? "null");
        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, true, shouldSave: false));

        Log.Debug("ScheduleEffects: HandleTrackSelected - Synced CurrentMusic to CurrentSchedule for ScheduleId: {ScheduleId}",
            scheduleId);
    }
}

