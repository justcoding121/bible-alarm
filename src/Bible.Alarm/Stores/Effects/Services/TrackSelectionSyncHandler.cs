#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Shared.Models.Enums;
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
                "CurrentBiblePublicationSchedule: {CurrentBiblePublicationSchedule}, TrackNumber={TrackNumber}, TrackTitle={TrackTitle}, " +
                "SectionNumber={SectionNumber}, PublicationCode={PublicationCode}",
                actionPub != null ? "not null" : "null",
                actionPub?.TrackNumber ?? -1,
                actionPub?.TrackTitle ?? "(null)",
                actionPub?.SectionNumber ?? -1,
                actionPub?.PublicationCode ?? "(null)");

            var currentState = state?.Value;
            if (currentState?.CurrentSchedule == null || action.CurrentBiblePublicationSchedule == null)
            {
                Log.Warning("TrackSelectionSyncHandler: HandleTrackSelected (BiblePublication) - CurrentSchedule or CurrentBiblePublicationSchedule is null.");
                return;
            }

            var currentSchedule = currentState.CurrentSchedule;
            var biblePub = action.CurrentBiblePublicationSchedule;

            // The reducer OnBiblePublicationTrackSelected now updates CurrentSchedule synchronously,
            // so we should check if CurrentSchedule already has the action's values.
            // If everything is already in sync, skip the redundant dispatch to avoid extra state updates.
            var alreadyInSync = 
                currentSchedule.BiblePublicationLanguageCode == biblePub.LanguageCode &&
                currentSchedule.BiblePublicationCode == biblePub.PublicationCode &&
                currentSchedule.BiblePublicationSectionNumber == biblePub.SectionNumber &&
                currentSchedule.BiblePublicationTrackNumber == biblePub.TrackNumber;
            
            if (alreadyInSync)
            {
                Log.Debug("TrackSelectionSyncHandler: HandleTrackSelected (BiblePublication) - CurrentSchedule already in sync with action, skipping dispatch.");
                return;
            }

            // Detect if language or publication changed - if so, we should NOT preserve old names
            var languageChanged = !string.IsNullOrEmpty(biblePub.LanguageCode) && 
                                  biblePub.LanguageCode != currentSchedule.BiblePublicationLanguageCode;
            var publicationChanged = !string.IsNullOrEmpty(biblePub.PublicationCode) && 
                                     biblePub.PublicationCode != currentSchedule.BiblePublicationCode;

            // Create updated schedule with Bible publication properties
            var updatedSchedule = currentSchedule.DeepClone();
            updatedSchedule.BiblePublicationScheduleId = biblePub.Id > 0 ? biblePub.Id : currentSchedule.BiblePublicationScheduleId;
            updatedSchedule.BiblePublicationLanguageCode = biblePub.LanguageCode;
            updatedSchedule.BiblePublicationCode = biblePub.PublicationCode;
            updatedSchedule.BiblePublicationSectionNumber = biblePub.SectionNumber;
            updatedSchedule.BiblePublicationTrackNumber = biblePub.TrackNumber;
            // Reset progress to 0.0 when track is changed (by cascade or direct selection)
            updatedSchedule.BiblePublicationFinishedDuration = TimeSpan.Zero;
            
            // Copy display names from the action (populated from list items when user tapped)
            // When language or publication changes, always use new values (or clear if empty)
            if (languageChanged)
            {
                // Language changed - always use new values, don't preserve old ones
                updatedSchedule.BiblePublicationLanguageName = biblePub.LanguageName ?? string.Empty;
                updatedSchedule.BiblePublicationLanguageDirection = biblePub.LanguageDirection ?? "ltr";
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
                if (string.IsNullOrEmpty(updatedSchedule.BiblePublicationSectionName) && biblePub.SectionNumber > 0)
                {
                    Log.Warning("TrackSelectionSyncHandler: SectionName is empty after publication change but SectionNumber={SectionNumber} is valid. " +
                        "Publication={PublicationCode}. This may cause empty section row in UI.",
                        biblePub.SectionNumber, biblePub.PublicationCode);
                }
                if (string.IsNullOrEmpty(updatedSchedule.BiblePublicationTrackTitle) && biblePub.TrackNumber > 0)
                {
                    Log.Warning("TrackSelectionSyncHandler: TrackTitle is empty after publication change but TrackNumber={TrackNumber} is valid. " +
                        "Publication={PublicationCode}. This may cause empty track row in UI.",
                        biblePub.TrackNumber, biblePub.PublicationCode);
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
                "ScheduleId: {ScheduleId}, TrackNumber={TrackNumber}, TrackTitle={TrackTitle}, SectionNumber={SectionNumber}",
                updatedSchedule.Id, updatedSchedule.BiblePublicationTrackNumber, updatedSchedule.BiblePublicationTrackTitle, updatedSchedule.BiblePublicationSectionNumber);
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, true, shouldSave: false));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TrackSelectionSyncHandler: Error syncing CurrentBiblePublicationSchedule to CurrentSchedule");
        }
    }

    private void LogTrackSelectedStart(MusicTrackSelectedAction action)
    {
        Log.Information("ScheduleEffects: HandleTrackSelected - Received action. CurrentMusic: {CurrentMusic}, MusicType: {MusicType}, PublicationCode: {PublicationCode}, TrackNumber: {TrackNumber}",
            action.CurrentMusic != null ? "not null" : "null",
            action.CurrentMusic?.MusicType ?? MusicType.Music,
            action.CurrentMusic?.PublicationCode ?? "null",
            action.CurrentMusic?.TrackNumber ?? 0);
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

    private ScheduleStateItem CreateUpdatedScheduleFromTrackSelection(ScheduleStateItem currentSchedule, MusicTrackSelectedAction action, bool musicTypeChanged)
    {
        var updatedSchedule = CloneBasicScheduleProperties(currentSchedule);
        PreserveBiblePublicationProperties(updatedSchedule, currentSchedule);
        UpdateMusicProperties(updatedSchedule, currentSchedule, action.CurrentMusic!, musicTypeChanged);
        SetMusicDisplayNames(updatedSchedule, action.CurrentMusic!);
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
            NumberOfTracksToRead = currentSchedule.NumberOfTracksToRead,
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
        updatedSchedule.BiblePublicationSectionNumber = currentSchedule.BiblePublicationSectionNumber;
        updatedSchedule.BiblePublicationTrackNumber = currentSchedule.BiblePublicationTrackNumber;
        updatedSchedule.BiblePublicationFinishedDuration = currentSchedule.BiblePublicationFinishedDuration;
        updatedSchedule.BiblePublicationLanguageName = currentSchedule.BiblePublicationLanguageName;
        updatedSchedule.BiblePublicationName = currentSchedule.BiblePublicationName;
        updatedSchedule.BiblePublicationSectionName = currentSchedule.BiblePublicationSectionName;
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
        updatedSchedule.MusicLanguageDirection = currentSchedule.MusicLanguageDirection;
        updatedSchedule.MusicPublicationName = currentSchedule.MusicPublicationName;
        updatedSchedule.MusicTrackName = currentSchedule.MusicTrackName;
    }

    private void SetMusicDisplayNames(ScheduleStateItem updatedSchedule, MusicStateItem actionMusic)
    {
        // IMPORTANT: Use display names from the action (populated from list items when user tapped).
        // Do NOT query the database - display names are already available from the selection.
        updatedSchedule.MusicLanguageName = actionMusic.LanguageName;
        updatedSchedule.MusicLanguageDirection = actionMusic.LanguageDirection;
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

