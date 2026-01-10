#nullable enable
using Bible;
using Bible.Alarm.Common;
using Bible.Alarm.Stores.Actions.Bible;
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
    /// Effect: Sync CurrentBibleReadingSchedule to CurrentSchedule when TrackSelectedAction is dispatched.
    /// This ensures that when sub-pages update CurrentBibleReadingSchedule, CurrentSchedule is also updated
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
            if (!ShouldSyncBibleReadingSchedule(currentSchedule, action.CurrentBibleReadingSchedule!))
            {
                return;
            }

            var updatedSchedule = CreateUpdatedScheduleFromTrackSelection(currentSchedule, action);
            DispatchUpdateAction(dispatcher, updatedSchedule, currentSchedule.Id);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error syncing CurrentBibleReadingSchedule to CurrentSchedule");
        }
    }

    private void LogTrackSelectedStart(TrackSelectedAction action)
    {
        Log.Information("ScheduleEffects: HandleTrackSelected - Received action. CurrentBibleReadingSchedule: {BibleReadingSchedule}, LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}, SectionNumber: {SectionNumber}, TrackNumber: {TrackNumber}",
            action.CurrentBibleReadingSchedule != null ? "not null" : "null",
            action.CurrentBibleReadingSchedule?.LanguageCode ?? "null",
            action.CurrentBibleReadingSchedule?.PublicationCode ?? "null",
            action.CurrentBibleReadingSchedule?.SectionNumber ?? 0,
            action.CurrentBibleReadingSchedule?.TrackNumber ?? 0);
    }

    private bool CanSyncTrackSelection(ApplicationState? currentState, TrackSelectedAction action)
    {
        if (currentState?.CurrentSchedule == null || action.CurrentBibleReadingSchedule == null)
        {
            Log.Warning("ScheduleEffects: HandleTrackSelected - CurrentSchedule or CurrentBibleReadingSchedule is null. CurrentSchedule: {CurrentSchedule}, CurrentBibleReadingSchedule: {BibleReadingSchedule}",
                currentState?.CurrentSchedule != null ? "not null" : "null",
                action.CurrentBibleReadingSchedule != null ? "not null" : "null");
            return false;
        }
        return true;
    }

    private bool ShouldSyncBibleReadingSchedule(ScheduleStateItem currentSchedule, BibleReadingStateItem actionBibleReadingSchedule)
    {
        Log.Debug("ScheduleEffects: HandleTrackSelected - CurrentSchedule Id: {ScheduleId}, BibleReadingScheduleId: {BibleReadingScheduleId}, Action BibleReadingSchedule Id: {ActionBibleReadingScheduleId}",
            currentSchedule.Id, currentSchedule.BibleReadingScheduleId, actionBibleReadingSchedule.Id);

        // Allow syncing if:
        // 1. Action has Id=0 (new selection, not yet saved) - always sync to update current schedule
        // 2. Action Id matches current schedule's BibleReadingScheduleId - same schedule, sync
        // Reject only if action has a non-zero ID that doesn't match (different schedule)
        if (actionBibleReadingSchedule.Id > 0 &&
            currentSchedule.BibleReadingScheduleId.HasValue &&
            actionBibleReadingSchedule.Id != currentSchedule.BibleReadingScheduleId.Value)
        {
            Log.Warning("ScheduleEffects: HandleTrackSelected - Different BibleReadingSchedule ID. Current: {CurrentId}, Action: {ActionId}. Not syncing.",
                currentSchedule.BibleReadingScheduleId.Value, actionBibleReadingSchedule.Id);
            return false;
        }

        Log.Debug("ScheduleEffects: HandleTrackSelected - Syncing allowed. Action Id: {ActionId} (0=new selection), Current BibleReadingScheduleId: {CurrentId}",
            actionBibleReadingSchedule.Id, currentSchedule.BibleReadingScheduleId);
        return true;
    }

    private ScheduleStateItem CreateUpdatedScheduleFromTrackSelection(ScheduleStateItem currentSchedule, TrackSelectedAction action)
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
            NumberOfTracksToRead = currentSchedule.NumberOfTracksToRead,
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
        updatedSchedule.BibleReadingSectionNumber = actionBibleReadingSchedule.SectionNumber;
        updatedSchedule.BibleReadingTrackNumber = actionBibleReadingSchedule.TrackNumber;
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
        updatedSchedule.BibleReadingSectionName = actionBibleReadingSchedule.SectionName;

        Log.Debug("ScheduleEffects: HandleTrackSelected - Using display names from action. LanguageName: {LanguageName}, PublicationName: {PublicationName}, SectionName: {SectionName}",
            updatedSchedule.BibleReadingLanguageName ?? "null",
            updatedSchedule.BibleReadingPublicationName ?? "null",
            updatedSchedule.BibleReadingSectionName ?? "null");
    }

    private void DispatchUpdateAction(IDispatcher dispatcher, ScheduleStateItem updatedSchedule, int scheduleId)
    {
        Log.Information("ScheduleEffects: HandleTrackSelected - Dispatching UpdateScheduleFromViewModelAction. ScheduleId: {ScheduleId}, LanguageCode: {LanguageCode}, LanguageName: {LanguageName}, PublicationCode: {PublicationCode}, PublicationName: {PublicationName}, SectionNumber: {SectionNumber}, SectionName: {SectionName}, TrackNumber: {TrackNumber}",
            updatedSchedule.Id,
            updatedSchedule.BibleReadingLanguageCode,
            updatedSchedule.BibleReadingLanguageName ?? "null",
            updatedSchedule.BibleReadingPublicationCode,
            updatedSchedule.BibleReadingPublicationName ?? "null",
            updatedSchedule.BibleReadingSectionNumber,
            updatedSchedule.BibleReadingSectionName ?? "null",
            updatedSchedule.BibleReadingTrackNumber);
        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, true, shouldSave: false));

        Log.Debug("ScheduleEffects: HandleTrackSelected - Synced CurrentBibleReadingSchedule to CurrentSchedule for ScheduleId: {ScheduleId}",
            scheduleId);
    }
}

