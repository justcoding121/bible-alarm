#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
using BiblePublicationTrackSelectedAction = Bible.Alarm.Stores.Actions.BiblePublications.TrackSelectedAction;
using MusicTrackSelectedAction = Bible.Alarm.Stores.Actions.Music.TrackSelectedAction;

namespace Bible.Alarm.Stores.Effects.Services;

/// <summary>
/// Handles syncing of track selection to CurrentSchedule.
/// Separated from ScheduleEffects for better modularity.
/// </summary>
public sealed class TrackSelectionSyncHandler
{
    private readonly IState<ApplicationState>? state;

    public TrackSelectionSyncHandler(IState<ApplicationState>? state = null)
    {
        this.state = state ?? ServiceProviderManager.GetService<IState<ApplicationState>>();
    }

    /// <summary>
    /// Effect: Sync CurrentMusic to CurrentSchedule when TrackSelectedAction is dispatched.
    /// This ensures that when sub-pages update CurrentMusic, CurrentSchedule is also updated
    /// so the schedule page displays the changes immediately.
    /// </summary>
    public async Task HandleTrackSelected(MusicTrackSelectedAction action, IDispatcher dispatcher)
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
            var languageCodeChanged = HasLanguageCodeChanged(currentSchedule, action.CurrentMusic!);

            if (!ShouldSyncMusic(currentSchedule, action.CurrentMusic!, languageCodeChanged))
            {
                return;
            }

            if (languageCodeChanged)
            {
                Log.Information("ScheduleEffects: HandleTrackSelected - Music language changed from {OldLang} to {NewLang}. Syncing.",
                    currentSchedule.MusicLanguageCode, action.CurrentMusic.LanguageCode);
            }

            var updatedSchedule = await CreateUpdatedScheduleFromTrackSelectionAsync(currentSchedule, action, languageCodeChanged);
            DispatchTrackUpdateAction(dispatcher, updatedSchedule, currentSchedule.Id);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error syncing CurrentMusic to CurrentSchedule");
        }
    }

    /// <summary>
    /// Effect: Sync CurrentBiblePublicationSchedule to CurrentSchedule when TrackSelectedAction is dispatched.
    /// This ensures that when sub-pages update CurrentBiblePublicationSchedule, CurrentSchedule is also updated
    /// so the schedule page displays the changes immediately.
    /// </summary>
    public async Task HandleTrackSelected(BiblePublicationTrackSelectedAction action, IDispatcher dispatcher)
    {
        try
        {
            var actionPub = action.CurrentBiblePublicationSchedule;
            Log.Information("TrackSelectionSyncHandler: HandleTrackSelected (BiblePublication) - Received action. " +
                "CurrentBiblePublicationSchedule: {CurrentBiblePublicationSchedule}, TrackCode={TrackCode}, TrackTitle={TrackTitle}, " +
                "SectionCode={SectionCode}, PublicationCode={PublicationCode}",
                actionPub != null ? "not null" : "null",
                actionPub?.TrackCode ?? "(null)",
                actionPub?.TrackTitle ?? "(null)",
                actionPub?.SectionCode ?? (actionPub?.SectionCode?.ToString() ?? "(null)"),
                actionPub?.PublicationCode ?? "(null)");

            var currentState = state?.Value;
            if (currentState?.CurrentSchedule == null || action.CurrentBiblePublicationSchedule == null)
            {
                Log.Warning("TrackSelectionSyncHandler: HandleTrackSelected (BiblePublication) - CurrentSchedule or CurrentBiblePublicationSchedule is null.");
                return;
            }

            var currentSchedule = currentState.CurrentSchedule;
            var biblePub = action.CurrentBiblePublicationSchedule;
            var currentSectionCode = currentSchedule.BiblePublicationSectionCode;
            var actionSectionCode = biblePub.SectionCode;

            // The reducer OnBiblePublicationTrackSelected now updates CurrentSchedule synchronously,
            // so we should check if CurrentSchedule already has the action's values.
            // If everything is already in sync, skip the redundant dispatch to avoid extra state updates.
            var alreadyInSync = 
                currentSchedule.BiblePublicationLanguageCode == biblePub.LanguageCode &&
                currentSchedule.BiblePublicationCode == biblePub.PublicationCode &&
                string.Equals(currentSectionCode, actionSectionCode, StringComparison.OrdinalIgnoreCase) &&
                currentSchedule.BiblePublicationTrackCode == biblePub.TrackCode;
            
            if (alreadyInSync)
            {
                Log.Debug("TrackSelectionSyncHandler: HandleTrackSelected (BiblePublication) - CurrentSchedule already in sync with action, skipping dispatch.");
                return;
            }

            // Detect if language or publication changed - if so, we should NOT preserve old names
            // Language code can ONLY be changed by:
            // 1. Category change (will default language to E)
            // 2. By user explicitly changing the language
            // 
            // IMPORTANT: When user selects a publication with no language (null), the current language stays the same.
            // The publication's language code (null) will be used in queries, but the schedule's language code doesn't change.
            var languageChanged = !string.IsNullOrEmpty(biblePub.LanguageCode) && 
                                  !string.IsNullOrEmpty(currentSchedule.BiblePublicationLanguageCode) &&
                                  biblePub.LanguageCode != currentSchedule.BiblePublicationLanguageCode;
            
            var publicationChanged = !string.IsNullOrEmpty(biblePub.PublicationCode) && 
                                     biblePub.PublicationCode != currentSchedule.BiblePublicationCode;

            // Create updated schedule with Bible publication properties
            var updatedSchedule = currentSchedule.DeepClone();
            updatedSchedule.BiblePublicationScheduleId = biblePub.Id > 0 ? biblePub.Id : currentSchedule.BiblePublicationScheduleId;
            
            // ALWAYS preserve category - category can only be changed via CategorySelectionAction
            // DeepClone() already preserves the category, but explicitly ensure it's never null/empty
            // EXCEPTION: If category is null in current schedule but BiblePublicationStateItem has one, use it
            // This handles cases where DispatchDefaultPublicationAsync is called and the category needs to be set
            if (string.IsNullOrWhiteSpace(updatedSchedule.BiblePublicationCategoryName))
            {
                // Category is null - try to get it from BiblePublicationStateItem
                if (!string.IsNullOrWhiteSpace(biblePub.CategoryName))
                {
                    updatedSchedule.BiblePublicationCategoryId = biblePub.CategoryId;
                    updatedSchedule.BiblePublicationCategoryName = biblePub.CategoryName;
                    Log.Debug("TrackSelectionSyncHandler: Set category={CategoryName} from BiblePublicationStateItem (was null)",
                        biblePub.CategoryName);
                }
                else
                {
                    // Category is null in both - this should not happen, but preserve what we can
                    Log.Error("TrackSelectionSyncHandler: Category is null in both current schedule and BiblePublicationStateItem. Category must always be selected.");
                }
            }
            else
            {
                // Category exists - ALWAYS preserve it (DeepClone already did this, but be explicit)
                // Category can only be changed via CategorySelectionAction
                // Ensure CategoryId is also preserved
                if (!updatedSchedule.BiblePublicationCategoryId.HasValue && biblePub.CategoryId.HasValue)
                {
                    // CategoryId might be missing even though CategoryName exists - preserve it
                    updatedSchedule.BiblePublicationCategoryId = biblePub.CategoryId;
                }
            }
            
            // Language code can ONLY be changed by:
            // 1. Category change (will default language to E)
            // 2. By user explicitly changing the language
            // 
            // IMPORTANT: When user selects a publication with no language (null), the current language stays the same.
            // The publication's language code (null) will be used in queries, but the schedule's language code doesn't change.
            if (languageChanged)
            {
                // Language changed - update language code and display names
                // This only happens when: category change or user explicitly changed the language
                updatedSchedule.BiblePublicationLanguageCode = biblePub.LanguageCode;
                updatedSchedule.BiblePublicationLanguageName = !string.IsNullOrEmpty(biblePub.LanguageName) 
                    ? biblePub.LanguageName 
                    : updatedSchedule.BiblePublicationLanguageName ?? string.Empty;
                updatedSchedule.BiblePublicationLanguageDirection = !string.IsNullOrEmpty(biblePub.LanguageDirection) 
                    ? biblePub.LanguageDirection 
                    : updatedSchedule.BiblePublicationLanguageDirection ?? AppConstants.Media.TextDirectionLeftToRight;
            }
            else
            {
                // Language did not change - ALWAYS preserve language (code, name, direction)
                // Language can only be changed via CategorySelectionAction or explicit user language selection
                // Even when selecting a publication without language, the current language stays the same
            }
            
            updatedSchedule.BiblePublicationCode = biblePub.PublicationCode;
            updatedSchedule.BiblePublicationSectionCode = actionSectionCode;
            updatedSchedule.BiblePublicationTrackCode = biblePub.TrackCode;
            // Do NOT reset progress here.
            // Progress reset (BiblePublicationFinishedDuration/FInishedDuration) is only persisted on Save,
            // after comparing the saved track identity to the originally-opened one.
            
            // Copy display names from the action (populated from list items when user tapped)
            // When language or publication changes, always use new values (or clear if empty)
            if (languageChanged)
            {
                // Language changed - update display names
                updatedSchedule.BiblePublicationName = biblePub.PublicationName ?? string.Empty;
                updatedSchedule.BiblePublicationSectionName = biblePub.SectionName ?? string.Empty;
                updatedSchedule.BiblePublicationTrackTitle = biblePub.TrackTitle ?? string.Empty;
            }
            else if (publicationChanged)
            {
                // Publication changed - preserve language name, update rest
                updatedSchedule.BiblePublicationLanguageName = !string.IsNullOrEmpty(biblePub.LanguageName) 
                    ? biblePub.LanguageName 
                    : currentSchedule.BiblePublicationLanguageName;
                updatedSchedule.BiblePublicationLanguageDirection = !string.IsNullOrEmpty(biblePub.LanguageDirection) 
                    ? biblePub.LanguageDirection 
                    : currentSchedule.BiblePublicationLanguageDirection;
                updatedSchedule.BiblePublicationName = biblePub.PublicationName ?? string.Empty;
                updatedSchedule.BiblePublicationSectionName = biblePub.SectionName ?? string.Empty;
                updatedSchedule.BiblePublicationTrackTitle = biblePub.TrackTitle ?? string.Empty;
                
                // Warn if section/track names are empty for a publication change - this may cause empty UI rows
                if (string.IsNullOrEmpty(updatedSchedule.BiblePublicationSectionName) && !string.IsNullOrWhiteSpace(actionSectionCode))
                {
                    Log.Warning("TrackSelectionSyncHandler: SectionName is empty after publication change but SectionCode={SectionCode} is set. " +
                        "Publication={PublicationCode}. This may cause empty section row in UI.",
                        actionSectionCode, biblePub.PublicationCode);
                }
                if (string.IsNullOrEmpty(updatedSchedule.BiblePublicationTrackTitle) && !string.IsNullOrWhiteSpace(biblePub.TrackCode))
                {
                    Log.Warning("TrackSelectionSyncHandler: TrackTitle is empty after publication change but TrackCode={TrackCode} is valid. " +
                        "Publication={PublicationCode}. This may cause empty track row in UI.",
                        biblePub.TrackCode, biblePub.PublicationCode);
                }
            }
            else
            {
                // Same language and publication - preserve if new value is empty
                updatedSchedule.BiblePublicationLanguageName = !string.IsNullOrEmpty(biblePub.LanguageName) 
                    ? biblePub.LanguageName 
                    : currentSchedule.BiblePublicationLanguageName;
                updatedSchedule.BiblePublicationLanguageDirection = !string.IsNullOrEmpty(biblePub.LanguageDirection) 
                    ? biblePub.LanguageDirection 
                    : currentSchedule.BiblePublicationLanguageDirection;
                updatedSchedule.BiblePublicationName = !string.IsNullOrEmpty(biblePub.PublicationName) 
                    ? biblePub.PublicationName 
                    : currentSchedule.BiblePublicationName;
                updatedSchedule.BiblePublicationSectionName = !string.IsNullOrEmpty(biblePub.SectionName) 
                    ? biblePub.SectionName 
                    : currentSchedule.BiblePublicationSectionName;
                updatedSchedule.BiblePublicationTrackTitle = !string.IsNullOrEmpty(biblePub.TrackTitle) 
                    ? biblePub.TrackTitle 
                    : currentSchedule.BiblePublicationTrackTitle;
            }

            Log.Information("TrackSelectionSyncHandler: HandleTrackSelected (BiblePublication) - Dispatching UpdateScheduleFromViewModelAction. " +
                "ScheduleId: {ScheduleId}, TrackCode={TrackCode}, TrackTitle={TrackTitle}, SectionCode={SectionCode}",
                updatedSchedule.Id, updatedSchedule.BiblePublicationTrackCode, updatedSchedule.BiblePublicationTrackTitle, updatedSchedule.BiblePublicationSectionCode);
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, true, shouldSave: false));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TrackSelectionSyncHandler: Error syncing CurrentBiblePublicationSchedule to CurrentSchedule");
        }
    }

    private void LogTrackSelectedStart(MusicTrackSelectedAction action)
    {
        // Music type is inferred from LanguageCode: NULL/empty = instrumental (melody), otherwise = vocal
        Log.Information("ScheduleEffects: HandleTrackSelected - Received action. CurrentMusic: {CurrentMusic}, LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, TrackCode: {TrackCode}",
            action.CurrentMusic != null ? "not null" : "null",
            action.CurrentMusic?.LanguageCode ?? "null (melody)",
            action.CurrentMusic?.PublicationCode ?? "null",
            action.CurrentMusic?.TrackCode ?? "(null)");
    }

    private bool CanSyncTrackSelection(ApplicationState? currentState, MusicTrackSelectedAction action)
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

    private static bool HasLanguageCodeChanged(ScheduleStateItem currentSchedule, MusicStateItem actionMusic)
    {
        // Check if the language code changed (which also indicates music type change)
        // NULL/empty = melody/instrumental, non-empty = vocal
        var currentIsMelody = string.IsNullOrEmpty(currentSchedule.MusicLanguageCode);
        var actionIsMelody = string.IsNullOrEmpty(actionMusic.LanguageCode);
        return currentIsMelody != actionIsMelody || currentSchedule.MusicLanguageCode != actionMusic.LanguageCode;
    }

    private bool ShouldSyncMusic(ScheduleStateItem currentSchedule, MusicStateItem actionMusic, bool languageCodeChanged)
    {
        Log.Debug("ScheduleEffects: HandleTrackSelected - CurrentSchedule Id: {ScheduleId}, MusicId: {MusicId}, Action Music Id: {ActionMusicId}",
            currentSchedule.Id, currentSchedule.MusicId, actionMusic.Id);

        // Allow syncing if:
        // 1. Action has Id=0 (new selection, not yet saved) - always sync to update current schedule
        // 2. Action Id matches current schedule's MusicId - same schedule, sync
        // 3. Language code changed (e.g., Melodies -> Vocals) - always sync to update current schedule
        // Reject only if action has a non-zero ID that doesn't match (different schedule) AND language hasn't changed
        if (actionMusic.Id > 0 &&
            currentSchedule.MusicId.HasValue &&
            actionMusic.Id != currentSchedule.MusicId.Value &&
            !languageCodeChanged)
        {
            Log.Warning("ScheduleEffects: HandleTrackSelected - Different Music ID. Current: {CurrentId}, Action: {ActionId}. Not syncing.",
                currentSchedule.MusicId.Value, actionMusic.Id);
            return false;
        }

        Log.Debug("ScheduleEffects: HandleTrackSelected - Syncing allowed. Action Id: {ActionId} (0=new selection), Current MusicId: {CurrentId}",
            actionMusic.Id, currentSchedule.MusicId);
        return true;
    }

    private async Task<ScheduleStateItem> CreateUpdatedScheduleFromTrackSelectionAsync(ScheduleStateItem currentSchedule, MusicTrackSelectedAction action, bool languageCodeChanged)
    {
        var updatedSchedule = CloneBasicScheduleProperties(currentSchedule);
        PreserveBiblePublicationProperties(updatedSchedule, currentSchedule);
        UpdateMusicProperties(updatedSchedule, currentSchedule, action.CurrentMusic!, languageCodeChanged);
        await SetMusicDisplayNamesAsync(updatedSchedule, action.CurrentMusic!);
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
            NumberOfTracksToPlay = currentSchedule.NumberOfTracksToPlay,
            AlwaysPlayFromStart = currentSchedule.AlwaysPlayFromStart,
            CurrentPlayItem = currentSchedule.CurrentPlayItem,
            LatestAlarmNotificationId = currentSchedule.LatestAlarmNotificationId
        };
    }

    private static void PreserveBiblePublicationProperties(ScheduleStateItem updatedSchedule, ScheduleStateItem currentSchedule)
    {
        updatedSchedule.BiblePublicationScheduleId = currentSchedule.BiblePublicationScheduleId;
        updatedSchedule.BiblePublicationLanguageCode = currentSchedule.BiblePublicationLanguageCode;
        updatedSchedule.BiblePublicationCode = currentSchedule.BiblePublicationCode;
        updatedSchedule.BiblePublicationSectionCode = currentSchedule.BiblePublicationSectionCode;
        updatedSchedule.BiblePublicationSectionCode = currentSchedule.BiblePublicationSectionCode;
        updatedSchedule.BiblePublicationTrackCode = currentSchedule.BiblePublicationTrackCode;
        updatedSchedule.BiblePublicationFinishedDuration = currentSchedule.BiblePublicationFinishedDuration;
        updatedSchedule.BiblePublicationLanguageName = currentSchedule.BiblePublicationLanguageName;
        updatedSchedule.BiblePublicationName = currentSchedule.BiblePublicationName;
        updatedSchedule.BiblePublicationSectionName = currentSchedule.BiblePublicationSectionName;
    }

    private static void UpdateMusicProperties(ScheduleStateItem updatedSchedule, ScheduleStateItem currentSchedule, MusicStateItem actionMusic, bool languageCodeChanged)
    {
        // If language code changed or Id is 0 (new selection), set MusicId to null or action's Id
        // Otherwise preserve the existing MusicId
        updatedSchedule.MusicId = (languageCodeChanged || actionMusic.Id == 0)
            ? (actionMusic.Id > 0 ? (int?)actionMusic.Id : null)
            : currentSchedule.MusicId;
        updatedSchedule.MusicPublicationCode = actionMusic.PublicationCode;
        updatedSchedule.MusicLanguageCode = actionMusic.LanguageCode;
        updatedSchedule.MusicSectionCode = actionMusic.SectionCode;
        updatedSchedule.MusicTrackCode = actionMusic.TrackCode;
        updatedSchedule.MusicRepeat = actionMusic.Repeat;
        // Preserve music display names from current schedule (will be repopulated if needed)
        updatedSchedule.MusicLanguageName = currentSchedule.MusicLanguageName;
        updatedSchedule.MusicLanguageDirection = currentSchedule.MusicLanguageDirection;
        updatedSchedule.MusicPublicationName = currentSchedule.MusicPublicationName;
        updatedSchedule.MusicTrackName = currentSchedule.MusicTrackName;
    }

    private async Task SetMusicDisplayNamesAsync(ScheduleStateItem updatedSchedule, MusicStateItem actionMusic)
    {
        // Use display names from the action when present (same as Bible container).
        updatedSchedule.MusicLanguageName = actionMusic.LanguageName;
        updatedSchedule.MusicLanguageDirection = actionMusic.LanguageDirection;
        updatedSchedule.MusicPublicationName = actionMusic.PublicationName;
        updatedSchedule.MusicSectionName = actionMusic.SectionName;
        updatedSchedule.MusicTrackName = actionMusic.TrackName;

        // When switching from melody to vocal (e.g. osg), action may have LanguageCode but LanguageName null
        // (user tapped osg from merged list without a selected language). Resolve name so the row shows "English" not "E".
        if (string.IsNullOrWhiteSpace(updatedSchedule.MusicLanguageName) && !string.IsNullOrWhiteSpace(actionMusic.LanguageCode))
        {
            var mediaService = ServiceProviderManager.GetService<IMediaService>();
            var languageNameService = ServiceProviderManager.GetService<Bible.Alarm.Shared.Services.Media.Interfaces.ILanguageNameService>();
            try
            {
                if (mediaService != null)
                {
                    var languages = await mediaService.GetVocalMusicLanguages();
                    if (languages.TryGetValue(actionMusic.LanguageCode, out var language))
                    {
                        updatedSchedule.MusicLanguageDirection = language.Direction ?? AppConstants.Media.TextDirectionLeftToRight;
                        updatedSchedule.MusicLanguageName = languageNameService != null
                            ? await languageNameService.GetNameAsync(language.Id, AppConstants.Media.DefaultLanguageCode) ?? actionMusic.LanguageCode
                            : actionMusic.LanguageCode;
                    }
                    else
                    {
                        updatedSchedule.MusicLanguageName = actionMusic.LanguageCode;
                        updatedSchedule.MusicLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
                    }
                }
                else
                {
                    updatedSchedule.MusicLanguageName = actionMusic.LanguageCode;
                    updatedSchedule.MusicLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
                }
            }
            catch
            {
                updatedSchedule.MusicLanguageName = actionMusic.LanguageCode;
                updatedSchedule.MusicLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
            }
        }

        Log.Debug("ScheduleEffects: HandleTrackSelected - Using display names from action. LanguageName: {LanguageName}, PublicationName: {PublicationName}, SectionName: {SectionName}, TrackName: {TrackName}",
            updatedSchedule.MusicLanguageName ?? "null",
            updatedSchedule.MusicPublicationName ?? "null",
            updatedSchedule.MusicSectionName ?? "null",
            updatedSchedule.MusicTrackName ?? "null");
    }

    private void DispatchTrackUpdateAction(IDispatcher dispatcher, ScheduleStateItem updatedSchedule, int scheduleId)
    {
        // Music type is inferred from LanguageCode: NULL/empty = instrumental (melody), otherwise = vocal
        Log.Information("ScheduleEffects: HandleTrackSelected - Dispatching UpdateScheduleFromViewModelAction. ScheduleId: {ScheduleId}, LanguageCode: {LanguageCode}, LanguageName: {LanguageName}, PublicationCode: {PublicationCode}, PublicationName: {PublicationName}, TrackCode: {TrackCode}, TrackName: {TrackName}",
            updatedSchedule.Id,
            updatedSchedule.MusicLanguageCode ?? "null (melody)",
            updatedSchedule.MusicLanguageName ?? "null",
            updatedSchedule.MusicPublicationCode ?? "null",
            updatedSchedule.MusicPublicationName ?? "null",
            updatedSchedule.MusicTrackCode,
            updatedSchedule.MusicTrackName ?? "null");
        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, musicUpdated: true, biblePublicationUpdated: false, shouldSave: false));

        Log.Debug("ScheduleEffects: HandleTrackSelected - Synced CurrentMusic to CurrentSchedule for ScheduleId: {ScheduleId}",
            scheduleId);
    }
}

