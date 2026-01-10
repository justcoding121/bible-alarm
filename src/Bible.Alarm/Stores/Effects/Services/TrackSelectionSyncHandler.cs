#nullable enable
using Bible;
using Bible.Alarm.Common;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

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

    private static void PreserveBibleReadingProperties(ScheduleStateItem updatedSchedule, ScheduleStateItem currentSchedule)
    {
        updatedSchedule.BibleReadingScheduleId = currentSchedule.BibleReadingScheduleId;
        updatedSchedule.BibleReadingLanguageCode = currentSchedule.BibleReadingLanguageCode;
        updatedSchedule.BibleReadingPublicationCode = currentSchedule.BibleReadingPublicationCode;
        updatedSchedule.BibleReadingSectionNumber = currentSchedule.BibleReadingSectionNumber;
        updatedSchedule.BibleReadingTrackNumber = currentSchedule.BibleReadingTrackNumber;
        updatedSchedule.BibleReadingFinishedDuration = currentSchedule.BibleReadingFinishedDuration;
        updatedSchedule.BibleReadingLanguageName = currentSchedule.BibleReadingLanguageName;
        updatedSchedule.BibleReadingPublicationName = currentSchedule.BibleReadingPublicationName;
        updatedSchedule.BibleReadingSectionName = currentSchedule.BibleReadingSectionName;
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

