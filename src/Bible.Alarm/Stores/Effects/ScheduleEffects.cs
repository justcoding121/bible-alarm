#nullable enable
using System;
using AutoMapper;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
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
public class ScheduleEffects
{
    private readonly IMapper _mapper;
    private readonly IBibleTranslationService? _bibleTranslationService;
    private readonly IBibleBookService? _bibleBookService;
    private readonly IAlarmScheduleService? _alarmScheduleService;
    private readonly IAlarmService? _alarmService;
    private readonly IMediaCacheService? _mediaCacheService;
    private readonly IState<ApplicationState>? _state;

    public ScheduleEffects(
        IMapper mapper, 
        IBibleTranslationService? bibleTranslationService = null,
        IBibleBookService? bibleBookService = null,
        IAlarmScheduleService? alarmScheduleService = null,
        IAlarmService? alarmService = null,
        IMediaCacheService? mediaCacheService = null,
        IState<ApplicationState>? state = null)
    {
        _mapper = mapper;
        _bibleTranslationService = bibleTranslationService ?? ServiceProviderManager.GetService<IBibleTranslationService>();
        _bibleBookService = bibleBookService ?? ServiceProviderManager.GetService<IBibleBookService>();
        _alarmScheduleService = alarmScheduleService ?? ServiceProviderManager.GetService<IAlarmScheduleService>();
        _alarmService = alarmService ?? ServiceProviderManager.GetService<IAlarmService>();
        _mediaCacheService = mediaCacheService ?? ServiceProviderManager.GetService<IMediaCacheService>();
        _state = state ?? ServiceProviderManager.GetService<IState<ApplicationState>>();
    }

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
            var scheduleStateItem = _mapper.Map<ScheduleStateItem>(action.Schedule);

            // Populate TranslationName and BookName if BibleReadingSchedule exists
            await PopulateTranslationNameAsync(scheduleStateItem, action.Schedule);
            await PopulateBookNameAsync(scheduleStateItem, action.Schedule);

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
            var scheduleStateItem = _mapper.Map<ScheduleStateItem>(action.Schedule);

            // Populate TranslationName and BookName if missing
            // (Note: This is for UpdateScheduleAction which doesn't go through the optimistic reducer)
            if (string.IsNullOrWhiteSpace(scheduleStateItem.TranslationName))
            {
                await PopulateTranslationNameAsync(scheduleStateItem, action.Schedule);
            }
            if (string.IsNullOrWhiteSpace(scheduleStateItem.BookName))
            {
                await PopulateBookNameAsync(scheduleStateItem, action.Schedule);
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

            if (action.Schedule == null || _alarmScheduleService == null)
            {
                Log.Warning("ScheduleEffects: HandleCreateSchedule - Schedule is null or service unavailable, skipping");
                if (action.Schedule != null)
                {
                    dispatcher.Dispatch(new CreateScheduleFailureAction(action.Schedule, "Service unavailable"));
                }
                return;
            }

            // Map domain model (ScheduleStateItem) → DB entity (AlarmSchedule)
            var dbSchedule = _mapper.Map<AlarmSchedule>(action.Schedule);
            
            Log.Debug("ScheduleEffects: HandleCreateSchedule - Before save. PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                dbSchedule.BibleReadingSchedule?.PublicationCode ?? "null",
                dbSchedule.BibleReadingSchedule?.LanguageCode ?? "null");
            
            // Set ID to 0 for new schedule (EF Core will generate it)
            dbSchedule.Id = 0;

            // Save to database
            var savedSchedule = await _alarmScheduleService.AddScheduleAsync(dbSchedule, CancellationToken.None);
            
            Log.Debug("ScheduleEffects: HandleCreateSchedule - After save. PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                savedSchedule.BibleReadingSchedule?.PublicationCode ?? "null",
                savedSchedule.BibleReadingSchedule?.LanguageCode ?? "null");
            
            Log.Information("ScheduleEffects: HandleCreateSchedule - Saved to DB. ScheduleId: {ScheduleId}", savedSchedule.Id);

            // Create alarm if enabled
            if (savedSchedule.IsEnabled && _alarmService != null)
            {
                await _alarmService.Create(savedSchedule);
            }

            // Map DB entity → domain model (ScheduleStateItem)
            var scheduleStateItem = _mapper.Map<ScheduleStateItem>(savedSchedule);
            
            // Populate TranslationName and BookName if BibleReadingSchedule exists
            await PopulateTranslationNameAsync(scheduleStateItem, savedSchedule);
            await PopulateBookNameAsync(scheduleStateItem, savedSchedule);

            // Dispatch success action with DTO
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
            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - ScheduleId: {ScheduleId}, Name: {Name}",
                action.Schedule?.Id, action.Schedule?.Name);

            if (action.Schedule == null || _alarmScheduleService == null)
            {
                Log.Warning("ScheduleEffects: HandleUpdateScheduleFromViewModel - Schedule is null or service unavailable, skipping");
                if (action.Schedule != null)
                {
                    dispatcher.Dispatch(new UpdateScheduleFailureAction(action.Schedule, "Service unavailable"));
                }
                return;
            }

            // Map domain model (ScheduleStateItem) → DB entity (AlarmSchedule)
            var dbSchedule = _mapper.Map<AlarmSchedule>(action.Schedule);

            // Update in database using UpdateScheduleByIdAsync to handle nested entities properly
            var savedSchedule = await _alarmScheduleService.UpdateScheduleByIdAsync(
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
                    if (action.MusicUpdated && dbSchedule.Music != null)
                    {
                        if (existing.Music == null)
                        {
                            existing.Music = dbSchedule.Music;
                            existing.Music.AlarmScheduleId = existing.Id;
                        }
                        else
                        {
                            existing.Music.Repeat = dbSchedule.Music.Repeat;
                            existing.Music.LanguageCode = dbSchedule.Music.LanguageCode;
                            existing.Music.MusicType = dbSchedule.Music.MusicType;
                            existing.Music.PublicationCode = dbSchedule.Music.PublicationCode;
                            existing.Music.TrackNumber = dbSchedule.Music.TrackNumber;
                        }
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

            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Updated in DB. ScheduleId: {ScheduleId}", savedSchedule.Id);

            // Update alarm
            if (_alarmService != null)
            {
                await Task.Run(() => _alarmService.Update(savedSchedule));
            }

            // Map DB entity → domain model (ScheduleStateItem)
            var scheduleStateItem = _mapper.Map<ScheduleStateItem>(savedSchedule);

            // TranslationName and BookName are preserved by the reducer during optimistic update.
            // Here we need to repopulate them if:
            // 1. They're missing/null
            // 2. Language code changed (TranslationName needs update)
            // 3. Book number, language code, or publication code changed (BookName needs update)
            var existingItem = _state?.Value.Schedules?.FirstOrDefault(s => s.Id == action.Schedule.Id);
            
            if (existingItem != null)
            {
                // Check if language code changed
                var languageCodeChanged = !string.Equals(
                    existingItem.BibleReadingLanguageCode ?? string.Empty,
                    scheduleStateItem.BibleReadingLanguageCode ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
                
                // Check if book-related codes changed
                var bookNumberChanged = existingItem.BibleReadingBookNumber != scheduleStateItem.BibleReadingBookNumber;
                var publicationCodeChanged = !string.Equals(
                    existingItem.BibleReadingPublicationCode ?? string.Empty,
                    scheduleStateItem.BibleReadingPublicationCode ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
                
                // Repopulate TranslationName if missing or language code changed
                if (string.IsNullOrWhiteSpace(scheduleStateItem.TranslationName) || 
                    languageCodeChanged ||
                    scheduleStateItem.TranslationName == scheduleStateItem.BibleReadingLanguageCode)
                {
                    await PopulateTranslationNameAsync(scheduleStateItem, savedSchedule);
                }

                // Repopulate BookName if missing or any book-related code changed
                if (string.IsNullOrWhiteSpace(scheduleStateItem.BookName) ||
                    bookNumberChanged ||
                    languageCodeChanged ||
                    publicationCodeChanged)
                {
                    await PopulateBookNameAsync(scheduleStateItem, savedSchedule);
                }
            }
            else
            {
                // No existing item - populate both
                await PopulateTranslationNameAsync(scheduleStateItem, savedSchedule);
                await PopulateBookNameAsync(scheduleStateItem, savedSchedule);
            }

            // Dispatch success action with DTO
            dispatcher.Dispatch(new UpdateScheduleSuccessAction(scheduleStateItem));

            Log.Information("ScheduleEffects: HandleUpdateScheduleFromViewModel - Dispatched UpdateScheduleSuccessAction for ScheduleId: {ScheduleId}",
                scheduleStateItem.Id);
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

            if (_alarmScheduleService == null)
            {
                Log.Warning("ScheduleEffects: HandleDeleteSchedule - Service unavailable, skipping");
                dispatcher.Dispatch(new DeleteScheduleFailureAction(action.ScheduleId, "Service unavailable"));
                return;
            }

            // Check if this is the last schedule - prevent deletion if it is
            var allSchedules = await _alarmScheduleService.GetAllSchedulesAsync(
                includeMusic: false,
                includeBibleReading: false,
                CancellationToken.None);

            if (allSchedules.Count <= 1)
            {
                Log.Warning("ScheduleEffects: HandleDeleteSchedule - Cannot delete schedule {ScheduleId} - it is the last schedule", action.ScheduleId);
                // Show toast message to user
                WeakReferenceMessenger.Default.Send(new Common.Messenger.ShowToastMessage("Cannot delete last schedule"));
                dispatcher.Dispatch(new DeleteScheduleFailureAction(action.ScheduleId, "Cannot delete last schedule"));
                return;
            }

            // Delete cached media files for this schedule
            if (_mediaCacheService != null)
            {
                await _mediaCacheService.DeleteScheduleCacheAsync(action.ScheduleId);
            }

            // Delete alarm notification
            if (_alarmService != null)
            {
                await Task.Run(() => _alarmService.Delete(action.ScheduleId));
            }

            // Delete from database
            await _alarmScheduleService.DeleteScheduleAsync(action.ScheduleId, CancellationToken.None);

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
    /// Populate TranslationName from language dictionary if BibleReadingSchedule exists.
    /// </summary>
    private async Task PopulateTranslationNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.BibleReadingSchedule == null || _bibleTranslationService == null)
            return;

        try
        {
            var languageCode = schedule.BibleReadingSchedule.LanguageCode;
            if (string.IsNullOrWhiteSpace(languageCode))
                return;

            var languagesDict = await _bibleTranslationService.GetDistinctLanguagesAsync();
            if (languagesDict.TryGetValue(languageCode, out var language))
            {
                scheduleStateItem.TranslationName = language.Name;
                Log.Debug("ScheduleEffects: Set TranslationName '{TranslationName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    language.Name, schedule.Id, languageCode);
            }
            else
            {
                scheduleStateItem.TranslationName = languageCode;
                Log.Debug("ScheduleEffects: Language not found for LanguageCode '{LanguageCode}', using code as TranslationName for schedule {ScheduleId}",
                    languageCode, schedule.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating TranslationName for schedule {ScheduleId}", schedule.Id);
            // Fallback to language code
            scheduleStateItem.TranslationName = schedule.BibleReadingSchedule.LanguageCode;
        }
    }

    /// <summary>
    /// Populate TranslationName from language dictionary using language code from ScheduleStateItem.
    /// </summary>
    private async Task PopulateTranslationNameAsync(ScheduleStateItem scheduleStateItem)
    {
        if (string.IsNullOrWhiteSpace(scheduleStateItem.BibleReadingLanguageCode) || _bibleTranslationService == null)
            return;

        try
        {
            var languagesDict = await _bibleTranslationService.GetDistinctLanguagesAsync();
            if (languagesDict.TryGetValue(scheduleStateItem.BibleReadingLanguageCode, out var language))
            {
                scheduleStateItem.TranslationName = language.Name;
                Log.Debug("ScheduleEffects: Set TranslationName '{TranslationName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    language.Name, scheduleStateItem.Id, scheduleStateItem.BibleReadingLanguageCode);
            }
            else
            {
                scheduleStateItem.TranslationName = scheduleStateItem.BibleReadingLanguageCode;
                Log.Debug("ScheduleEffects: Language not found for LanguageCode '{LanguageCode}', using code as TranslationName for schedule {ScheduleId}",
                    scheduleStateItem.BibleReadingLanguageCode, scheduleStateItem.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating TranslationName for schedule {ScheduleId}", scheduleStateItem.Id);
            // Fallback to language code
            scheduleStateItem.TranslationName = scheduleStateItem.BibleReadingLanguageCode;
        }
    }

    /// <summary>
    /// Populate BookName from BibleBookService if BibleReadingSchedule exists.
    /// </summary>
    private async Task PopulateBookNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.BibleReadingSchedule == null || _bibleBookService == null)
            return;

        try
        {
            var bibleReading = schedule.BibleReadingSchedule;
            if (bibleReading.BookNumber <= 0 || 
                string.IsNullOrWhiteSpace(bibleReading.LanguageCode) ||
                string.IsNullOrWhiteSpace(bibleReading.PublicationCode))
                return;

            var bookName = await _bibleBookService.GetBookNameAsync(
                bibleReading.LanguageCode,
                bibleReading.PublicationCode,
                bibleReading.BookNumber);
            
            if (!string.IsNullOrWhiteSpace(bookName))
            {
                scheduleStateItem.BookName = bookName;
                Log.Debug("ScheduleEffects: Set BookName '{BookName}' for schedule {ScheduleId} (BookNumber: {BookNumber})",
                    bookName, schedule.Id, bibleReading.BookNumber);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating BookName for schedule {ScheduleId}", schedule.Id);
        }
    }
}

