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
            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - ScheduleId: {ScheduleId}, Name: {Name}, ShouldSave: {ShouldSave}",
                action.Schedule?.Id, action.Schedule?.Name, action.ShouldSave);

            if (action.Schedule == null)
            {
                Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - Schedule is null, skipping");
                return;
            }

            // If shouldSave is false, only update state (optimistic update already done by reducer)
            // This is used for UI-only updates like next/previous chapter/book navigation
            if (!action.ShouldSave)
            {
                Log.Debug("ScheduleEffects: HandleUpdateScheduleFromViewModel - ShouldSave=false, skipping DB update. Only state was updated.");
                return;
            }

            if (alarmScheduleService == null)
            {
                Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - Service unavailable, skipping");
                dispatcher.Dispatch(new UpdateScheduleFailureAction(action.Schedule, "Service unavailable"));
                return;
            }

            // Map domain model (ScheduleStateItem) → DB entity (AlarmSchedule)
            var dbSchedule = mapper.Map<AlarmSchedule>(action.Schedule);

            // Update in database using UpdateScheduleByIdAsync to handle nested entities properly
            var savedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
                action.Schedule.Id,
                existing =>
                {
                    // Update schedule properties
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

                    // Only update music if it was changed
                    if (action.MusicUpdated)
                    {
                        if (dbSchedule.Music != null)
                        {
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
                        else if (action.Schedule != null && action.Schedule.MusicType.HasValue && action.Schedule.MusicTrackNumber.HasValue && action.Schedule.MusicTrackNumber.Value > 0)
                        {
                            // Fallback: dbSchedule.Music is null but we have music properties in action.Schedule
                            // This can happen when MusicId is null but MusicType is set (e.g., changing from Melodies to Vocals)
                            Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - action.MusicUpdated=true but dbSchedule.Music is null. Creating Music from action.Schedule. MusicType={MusicType}, TrackNumber={TrackNumber}",
                                action.Schedule.MusicType, action.Schedule.MusicTrackNumber);
                            
                            if (existing.Music == null)
                            {
                                existing.Music = new AlarmMusic
                                {
                                    Id = action.Schedule.MusicId ?? 0,
                                    MusicType = action.Schedule.MusicType.Value,
                                    PublicationCode = action.Schedule.MusicPublicationCode ?? string.Empty,
                                    LanguageCode = action.Schedule.MusicLanguageCode,
                                    TrackNumber = action.Schedule.MusicTrackNumber.Value,
                                    Repeat = action.Schedule.MusicRepeat ?? false,
                                    AlarmScheduleId = existing.Id
                                };
                                Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Created new Music entity from action.Schedule");
                            }
                            else
                            {
                                var oldMusicType = existing.Music.MusicType;
                                var oldTrackNumber = existing.Music.TrackNumber;
                                Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updating existing Music from action.Schedule. Old MusicType={OldMusicType}, Old TrackNumber={OldTrackNumber}",
                                    oldMusicType, oldTrackNumber);
                                
                                existing.Music.MusicType = action.Schedule.MusicType.Value;
                                existing.Music.PublicationCode = action.Schedule.MusicPublicationCode ?? string.Empty;
                                existing.Music.LanguageCode = action.Schedule.MusicLanguageCode;
                                existing.Music.TrackNumber = action.Schedule.MusicTrackNumber.Value;
                                existing.Music.Repeat = action.Schedule.MusicRepeat ?? false;
                                if (action.Schedule.MusicId.HasValue)
                                {
                                    existing.Music.Id = action.Schedule.MusicId.Value;
                                }
                                
                                Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updated Music from action.Schedule. New MusicType={NewMusicType}, New TrackNumber={NewTrackNumber}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                                    existing.Music.MusicType, existing.Music.TrackNumber, existing.Music.PublicationCode, existing.Music.LanguageCode);
                            }
                        }
                        else
                        {
                            Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - action.MusicUpdated=true but dbSchedule.Music is null and action.Schedule has no valid music properties");
                        }
                    }
                    else
                    {
                        Log.Debug("ScheduleEffects: HandleUpdateScheduleFromViewModel - action.MusicUpdated=false, skipping music update");
                    }

                    // Update BibleReadingSchedule if it exists
                    if (dbSchedule.BibleReadingSchedule != null)
                    {
                        if (existing.BibleReadingSchedule == null)
                        {
                            existing.BibleReadingSchedule = dbSchedule.BibleReadingSchedule;
                            existing.BibleReadingSchedule.AlarmScheduleId = existing.Id;
                        }
                        else
                        {
                            existing.BibleReadingSchedule.BookNumber = dbSchedule.BibleReadingSchedule.BookNumber;
                            existing.BibleReadingSchedule.ChapterNumber = dbSchedule.BibleReadingSchedule.ChapterNumber;
                            existing.BibleReadingSchedule.LanguageCode = dbSchedule.BibleReadingSchedule.LanguageCode;
                            existing.BibleReadingSchedule.PublicationCode = dbSchedule.BibleReadingSchedule.PublicationCode;
                            // Only reset duration if bible reading was changed
                            if (action.BibleReadingUpdated)
                            {
                                existing.BibleReadingSchedule.FinishedDuration = TimeSpan.Zero;
                            }
                        }
                    }
                },
                CancellationToken.None);

            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updated in DB. ScheduleId: {ScheduleId}, savedSchedule.Music={HasMusic}, savedSchedule.Music.MusicType={MusicType}, savedSchedule.Music.TrackNumber={TrackNumber}, savedSchedule.Music.PublicationCode={PublicationCode}, savedSchedule.Music.LanguageCode={LanguageCode}",
                savedSchedule.Id,
                savedSchedule.Music != null ? "not null" : "null",
                savedSchedule.Music?.MusicType.ToString() ?? "null",
                savedSchedule.Music?.TrackNumber.ToString() ?? "null",
                savedSchedule.Music?.PublicationCode ?? "null",
                savedSchedule.Music?.LanguageCode ?? "null");

            // Update alarm
            if (alarmService != null)
            {
                await Task.Run(() => alarmService.Update(savedSchedule));
            }

            // Map DB entity → domain model (ScheduleStateItem)
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(savedSchedule);
            
            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - After mapping savedSchedule to scheduleStateItem. scheduleStateItem.MusicType={MusicType}, scheduleStateItem.MusicTrackNumber={TrackNumber}, scheduleStateItem.MusicPublicationCode={PublicationCode}, scheduleStateItem.MusicLanguageCode={LanguageCode}",
                scheduleStateItem.MusicType?.ToString() ?? "null",
                scheduleStateItem.MusicTrackNumber?.ToString() ?? "null",
                scheduleStateItem.MusicPublicationCode ?? "null",
                scheduleStateItem.MusicLanguageCode ?? "null");

            // IMPORTANT: Display names are already populated in action.Schedule (from CurrentSchedule state).
            // Selection pages/containers populate display names when user selects items (via HandleChapterSelected/HandleTrackSelected effects).
            // We should NOT query the database here - just preserve the display names from the action.
            // Copy display names from action.Schedule to scheduleStateItem (which was mapped from savedSchedule, so it doesn't have display names)
            if (action.Schedule != null)
            {
                Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Copying display names from action.Schedule. action.Schedule.MusicType={MusicType}, action.Schedule.MusicTrackNumber={TrackNumber}",
                    action.Schedule.MusicType?.ToString() ?? "null",
                    action.Schedule.MusicTrackNumber?.ToString() ?? "null");
                
                scheduleStateItem.BibleReadingLanguageName = action.Schedule.BibleReadingLanguageName;
                scheduleStateItem.BibleReadingPublicationName = action.Schedule.BibleReadingPublicationName;
                scheduleStateItem.BibleReadingBookName = action.Schedule.BibleReadingBookName;
                scheduleStateItem.MusicLanguageName = action.Schedule.MusicLanguageName;
                scheduleStateItem.MusicPublicationName = action.Schedule.MusicPublicationName;
                scheduleStateItem.MusicTrackName = action.Schedule.MusicTrackName;
                
                // IMPORTANT: Also preserve music properties from action.Schedule if they differ from savedSchedule
                // This ensures music type changes are preserved even if mapping from savedSchedule loses them
                if (action.Schedule.MusicType.HasValue && 
                    (!scheduleStateItem.MusicType.HasValue || scheduleStateItem.MusicType.Value != action.Schedule.MusicType.Value))
                {
                    Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - MusicType mismatch! savedSchedule.Music.MusicType={SavedMusicType}, action.Schedule.MusicType={ActionMusicType}. Using action.Schedule.MusicType.",
                        scheduleStateItem.MusicType?.ToString() ?? "null",
                        action.Schedule.MusicType?.ToString() ?? "null");
                    scheduleStateItem.MusicType = action.Schedule.MusicType;
                    scheduleStateItem.MusicTrackNumber = action.Schedule.MusicTrackNumber;
                    scheduleStateItem.MusicPublicationCode = action.Schedule.MusicPublicationCode;
                    scheduleStateItem.MusicLanguageCode = action.Schedule.MusicLanguageCode;
                    scheduleStateItem.MusicRepeat = action.Schedule.MusicRepeat;
                    scheduleStateItem.MusicId = action.Schedule.MusicId;
                }
            }

            // Dispatch success action with DTO (display names preserved from state)
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
                dispatcher.Dispatch(new DeleteScheduleFailureAction(action.ScheduleId, "Cannot delete last schedule"));
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
            Log.Information("ScheduleEffects: HandleChapterSelected - Received action. CurrentBibleReadingSchedule: {BibleReadingSchedule}, LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, BookNumber: {BookNumber}, ChapterNumber: {ChapterNumber}",
                action.CurrentBibleReadingSchedule != null ? "not null" : "null",
                action.CurrentBibleReadingSchedule?.LanguageCode ?? "null",
                action.CurrentBibleReadingSchedule?.PublicationCode ?? "null",
                action.CurrentBibleReadingSchedule?.BookNumber ?? 0,
                action.CurrentBibleReadingSchedule?.ChapterNumber ?? 0);

            var currentState = state?.Value;
            if (currentState?.CurrentSchedule == null || action.CurrentBibleReadingSchedule == null)
            {
                Log.Warning("ScheduleEffects: HandleChapterSelected - CurrentSchedule or CurrentBibleReadingSchedule is null. CurrentSchedule: {CurrentSchedule}, CurrentBibleReadingSchedule: {BibleReadingSchedule}",
                    currentState?.CurrentSchedule != null ? "not null" : "null",
                    action.CurrentBibleReadingSchedule != null ? "not null" : "null");
                return;
            }

            // Only sync if the BibleReadingSchedule belongs to the current schedule
            var currentSchedule = currentState.CurrentSchedule;
            Log.Debug("ScheduleEffects: HandleChapterSelected - CurrentSchedule Id: {ScheduleId}, BibleReadingScheduleId: {BibleReadingScheduleId}, Action BibleReadingSchedule Id: {ActionBibleReadingScheduleId}",
                currentSchedule.Id, currentSchedule.BibleReadingScheduleId, action.CurrentBibleReadingSchedule.Id);

            // Allow syncing if:
            // 1. Action has Id=0 (new selection, not yet saved) - always sync to update current schedule
            // 2. Action Id matches current schedule's BibleReadingScheduleId - same schedule, sync
            // Reject only if action has a non-zero ID that doesn't match (different schedule)
            if (action.CurrentBibleReadingSchedule.Id > 0 && 
                currentSchedule.BibleReadingScheduleId.HasValue && 
                action.CurrentBibleReadingSchedule.Id != currentSchedule.BibleReadingScheduleId.Value)
            {
                // Different BibleReadingSchedule, don't sync
                Log.Warning("ScheduleEffects: HandleChapterSelected - Different BibleReadingSchedule ID. Current: {CurrentId}, Action: {ActionId}. Not syncing.",
                    currentSchedule.BibleReadingScheduleId.Value, action.CurrentBibleReadingSchedule.Id);
                return;
            }
            
            Log.Debug("ScheduleEffects: HandleChapterSelected - Syncing allowed. Action Id: {ActionId} (0=new selection), Current BibleReadingScheduleId: {CurrentId}",
                action.CurrentBibleReadingSchedule.Id, currentSchedule.BibleReadingScheduleId);

            // Clone CurrentSchedule and update Bible reading properties from CurrentBibleReadingSchedule
            var updatedSchedule = new ScheduleStateItem
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
                LatestAlarmNotificationId = currentSchedule.LatestAlarmNotificationId,
                BibleReadingScheduleId = action.CurrentBibleReadingSchedule.Id > 0 ? action.CurrentBibleReadingSchedule.Id : currentSchedule.BibleReadingScheduleId,
                BibleReadingLanguageCode = action.CurrentBibleReadingSchedule.LanguageCode,
                BibleReadingPublicationCode = action.CurrentBibleReadingSchedule.PublicationCode,
                BibleReadingBookNumber = action.CurrentBibleReadingSchedule.BookNumber,
                BibleReadingChapterNumber = action.CurrentBibleReadingSchedule.ChapterNumber,
                BibleReadingFinishedDuration = action.CurrentBibleReadingSchedule.FinishedDuration,
                MusicId = currentSchedule.MusicId,
                MusicType = currentSchedule.MusicType,
                MusicPublicationCode = currentSchedule.MusicPublicationCode,
                MusicLanguageCode = currentSchedule.MusicLanguageCode,
                MusicTrackNumber = currentSchedule.MusicTrackNumber,
                MusicRepeat = currentSchedule.MusicRepeat,
                // Preserve music display names from current schedule
                MusicLanguageName = currentSchedule.MusicLanguageName,
                MusicPublicationName = currentSchedule.MusicPublicationName,
                MusicTrackName = currentSchedule.MusicTrackName
            };

            // IMPORTANT: Use display names from the action (populated from list items when user tapped).
            // Do NOT query the database - display names are already available from the selection.
            updatedSchedule.BibleReadingLanguageName = action.CurrentBibleReadingSchedule.LanguageName;
            updatedSchedule.BibleReadingPublicationName = action.CurrentBibleReadingSchedule.PublicationName;
            updatedSchedule.BibleReadingBookName = action.CurrentBibleReadingSchedule.BookName;

            Log.Debug("ScheduleEffects: HandleChapterSelected - Using display names from action. LanguageName: {LanguageName}, PublicationName: {PublicationName}, BookName: {BookName}",
                updatedSchedule.BibleReadingLanguageName ?? "null",
                updatedSchedule.BibleReadingPublicationName ?? "null",
                updatedSchedule.BibleReadingBookName ?? "null");

            // Dispatch UpdateScheduleFromViewModelAction to sync to CurrentSchedule (optimistic update only, no DB save)
            // shouldSave=false because this is just syncing state, not a user-initiated save
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
                currentSchedule.Id);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error syncing CurrentBibleReadingSchedule to CurrentSchedule");
        }
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
            Log.Information("ScheduleEffects: HandleTrackSelected - Received action. CurrentMusic: {CurrentMusic}, MusicType: {MusicType}, PublicationCode: {PublicationCode}, TrackNumber: {TrackNumber}",
                action.CurrentMusic != null ? "not null" : "null",
                action.CurrentMusic?.MusicType ?? MusicType.Melodies,
                action.CurrentMusic?.PublicationCode ?? "null",
                action.CurrentMusic?.TrackNumber ?? 0);

            var currentState = state?.Value;
            if (currentState?.CurrentSchedule == null || action.CurrentMusic == null)
            {
                Log.Warning("ScheduleEffects: HandleTrackSelected - CurrentSchedule or CurrentMusic is null. CurrentSchedule: {CurrentSchedule}, CurrentMusic: {CurrentMusic}",
                    currentState?.CurrentSchedule != null ? "not null" : "null",
                    action.CurrentMusic != null ? "not null" : "null");
                return;
            }

            // Only sync if the Music belongs to the current schedule
            var currentSchedule = currentState.CurrentSchedule;
            Log.Debug("ScheduleEffects: HandleTrackSelected - CurrentSchedule Id: {ScheduleId}, MusicId: {MusicId}, Action Music Id: {ActionMusicId}",
                currentSchedule.Id, currentSchedule.MusicId, action.CurrentMusic.Id);

            // Allow syncing if:
            // 1. Action has Id=0 (new selection, not yet saved) - always sync to update current schedule
            // 2. Action Id matches current schedule's MusicId - same schedule, sync
            // 3. Music type changed (e.g., Melodies -> Vocals) - always sync to update current schedule
            // Reject only if action has a non-zero ID that doesn't match (different schedule) AND music type hasn't changed
            var musicTypeChanged = currentSchedule.MusicType.HasValue && 
                                   currentSchedule.MusicType.Value != action.CurrentMusic.MusicType;
            
            if (action.CurrentMusic.Id > 0 && 
                currentSchedule.MusicId.HasValue && 
                action.CurrentMusic.Id != currentSchedule.MusicId.Value &&
                !musicTypeChanged)
            {
                // Different Music and music type hasn't changed, don't sync
                Log.Warning("ScheduleEffects: HandleTrackSelected - Different Music ID. Current: {CurrentId}, Action: {ActionId}. Not syncing.",
                    currentSchedule.MusicId.Value, action.CurrentMusic.Id);
                return;
            }
            
            if (musicTypeChanged)
            {
                Log.Information("ScheduleEffects: HandleTrackSelected - Music type changed from {OldType} to {NewType}. Syncing.",
                    currentSchedule.MusicType, action.CurrentMusic.MusicType);
            }
            
            Log.Debug("ScheduleEffects: HandleTrackSelected - Syncing allowed. Action Id: {ActionId} (0=new selection), Current MusicId: {CurrentId}",
                action.CurrentMusic.Id, currentSchedule.MusicId);

            // Clone CurrentSchedule and update music properties from CurrentMusic
            var updatedSchedule = new ScheduleStateItem
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
                LatestAlarmNotificationId = currentSchedule.LatestAlarmNotificationId,
                BibleReadingScheduleId = currentSchedule.BibleReadingScheduleId,
                BibleReadingLanguageCode = currentSchedule.BibleReadingLanguageCode,
                BibleReadingPublicationCode = currentSchedule.BibleReadingPublicationCode,
                BibleReadingBookNumber = currentSchedule.BibleReadingBookNumber,
                BibleReadingChapterNumber = currentSchedule.BibleReadingChapterNumber,
                BibleReadingFinishedDuration = currentSchedule.BibleReadingFinishedDuration,
                // Preserve Bible reading display names from current schedule
                BibleReadingLanguageName = currentSchedule.BibleReadingLanguageName,
                BibleReadingPublicationName = currentSchedule.BibleReadingPublicationName,
                BibleReadingBookName = currentSchedule.BibleReadingBookName,
                // If music type changed or Id is 0 (new selection), set MusicId to null or action's Id
                // Otherwise preserve the existing MusicId
                MusicId = (musicTypeChanged || action.CurrentMusic.Id == 0) 
                    ? (action.CurrentMusic.Id > 0 ? action.CurrentMusic.Id : null)
                    : currentSchedule.MusicId,
                MusicType = action.CurrentMusic.MusicType,
                MusicPublicationCode = action.CurrentMusic.PublicationCode,
                MusicLanguageCode = action.CurrentMusic.LanguageCode,
                MusicTrackNumber = action.CurrentMusic.TrackNumber,
                MusicRepeat = action.CurrentMusic.Repeat,
                // Preserve music display names from current schedule (will be repopulated if needed)
                MusicLanguageName = currentSchedule.MusicLanguageName,
                MusicPublicationName = currentSchedule.MusicPublicationName,
                MusicTrackName = currentSchedule.MusicTrackName
            };

            // IMPORTANT: Use display names from the action (populated from list items when user tapped).
            // Do NOT query the database - display names are already available from the selection.
            updatedSchedule.MusicLanguageName = action.CurrentMusic.LanguageName;
            updatedSchedule.MusicPublicationName = action.CurrentMusic.PublicationName;
            updatedSchedule.MusicTrackName = action.CurrentMusic.TrackName;

            Log.Debug("ScheduleEffects: HandleTrackSelected - Using display names from action. LanguageName: {LanguageName}, PublicationName: {PublicationName}, TrackName: {TrackName}",
                updatedSchedule.MusicLanguageName ?? "null",
                updatedSchedule.MusicPublicationName ?? "null",
                updatedSchedule.MusicTrackName ?? "null");

            // Dispatch UpdateScheduleFromViewModelAction to sync to CurrentSchedule (optimistic update only, no DB save)
            // shouldSave=false because this is just syncing state, not a user-initiated save
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
                currentSchedule.Id);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error syncing CurrentMusic to CurrentSchedule");
        }
    }
}

