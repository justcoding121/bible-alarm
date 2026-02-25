#nullable enable
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Stores.Reducers.Services;

/// <summary>
/// Handles syncing of CurrentSchedule, CurrentMusic, and CurrentBiblePublicationSchedule.
/// Separated from ApplicationReducer for better modularity.
/// </summary>
public static class ScheduleStateSyncHelper
{
    public static ScheduleStateItem? UpdateCurrentScheduleIfMatches(ApplicationState state, ScheduleStateItem actionSchedule)
    {
        if (state.CurrentSchedule?.Id == actionSchedule.Id)
        {
            // Check if the schedule actually changed to prevent unnecessary state updates
            if (AreSchedulesEquivalent(state.CurrentSchedule, actionSchedule))
            {
                Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - CurrentSchedule values unchanged, returning existing reference to prevent cycle. ScheduleId: {ScheduleId}",
                    actionSchedule.Id);
                return state.CurrentSchedule;
            }

            Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Updating CurrentSchedule. New LanguageName: {LanguageName}, PublicationName: {PublicationName}",
                actionSchedule.BiblePublicationLanguageName ?? "null",
                actionSchedule.BiblePublicationName ?? "null");
            
            // IMPORTANT: Preserve display names (including category) from existing CurrentSchedule
            // This ensures category is never lost when updating CurrentSchedule
            // Category can only be changed via CategorySelectionAction
            DisplayNamePreservationHelper.PreserveDisplayNamesFromExisting(actionSchedule, state.CurrentSchedule);
            
            // Preserve basic schedule properties if action has invalid values but existing has valid values
            // This prevents cascade handlers or other updates from clearing DaysOfWeek, time, etc.
            var clonedSchedule = actionSchedule.DeepClone();
            PreserveBasicScheduleProperties(clonedSchedule, state.CurrentSchedule);
            
            return clonedSchedule;
        }

        Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - CurrentSchedule ID ({CurrentScheduleId}) doesn't match action Schedule ID ({ActionScheduleId}), not updating CurrentSchedule",
            state.CurrentSchedule?.Id ?? 0, actionSchedule.Id);
        return state.CurrentSchedule;
    }

    /// <summary>
    /// Checks if two schedules have equivalent values for the properties that matter for state updates.
    /// This helps prevent unnecessary state updates that cause cycles.
    /// </summary>
    private static bool AreSchedulesEquivalent(ScheduleStateItem current, ScheduleStateItem action)
    {
        var currentSectionCode = current.BiblePublicationSectionCode;
        var actionSectionCode = action.BiblePublicationSectionCode;

        // Compare key properties that would trigger state changes.
        // Category must be included so that changing category (e.g. Music → Bible) is not treated as unchanged.
        return current.Id == action.Id &&
               current.BiblePublicationCategoryId == action.BiblePublicationCategoryId &&
               string.Equals(current.BiblePublicationCategoryName, action.BiblePublicationCategoryName, StringComparison.OrdinalIgnoreCase) &&
               current.MusicLanguageCode == action.MusicLanguageCode &&
               current.MusicPublicationCode == action.MusicPublicationCode &&
               current.MusicTrackCode == action.MusicTrackCode &&
               current.MusicRepeat == action.MusicRepeat &&
               current.BiblePublicationLanguageCode == action.BiblePublicationLanguageCode &&
               current.BiblePublicationCode == action.BiblePublicationCode &&
               SectionCodeHelper.CodeEquals(currentSectionCode, actionSectionCode) &&
               current.BiblePublicationTrackCode == action.BiblePublicationTrackCode &&
               current.BiblePublicationModalItemCount == action.BiblePublicationModalItemCount &&
               current.BiblePublicationSectionModalItemCount == action.BiblePublicationSectionModalItemCount &&
               current.BiblePublicationTrackModalItemCount == action.BiblePublicationTrackModalItemCount &&
               current.MusicPublicationModalItemCount == action.MusicPublicationModalItemCount &&
               current.MusicSectionModalItemCount == action.MusicSectionModalItemCount &&
               current.Name == action.Name &&
               current.IsEnabled == action.IsEnabled &&
               current.NotificationEnabled == action.NotificationEnabled &&
               current.Hour == action.Hour &&
               current.Minute == action.Minute &&
               current.Second == action.Second &&
               current.DaysOfWeek == action.DaysOfWeek &&
               current.MusicEnabled == action.MusicEnabled &&
               current.NumberOfTracksToPlay == action.NumberOfTracksToPlay &&
               current.AlwaysPlayFromStart == action.AlwaysPlayFromStart;
    }

    public static BiblePublicationStateItem? SyncBiblePublicationScheduleIfNeeded(UpdateScheduleFromViewModelAction action, ScheduleStateItem? updatedCurrentSchedule)
    {
        if (!action.BiblePublicationUpdated || updatedCurrentSchedule == null)
        {
            return null;
        }

        if (!HasValidBiblePublicationProperties(updatedCurrentSchedule))
        {
            return null;
        }

        var biblePublicationSchedule = CreateBiblePublicationScheduleFromCurrent(updatedCurrentSchedule);
        Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Synced CurrentBiblePublicationSchedule from CurrentSchedule. LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}",
            biblePublicationSchedule.LanguageCode, biblePublicationSchedule.PublicationCode);
        return biblePublicationSchedule;
    }

    public static bool HasValidBiblePublicationProperties(ScheduleStateItem schedule)
    {
        if (string.IsNullOrWhiteSpace(schedule.BiblePublicationCode) ||
            string.IsNullOrWhiteSpace(schedule.BiblePublicationTrackCode))
        {
            return false;
        }

        var isMusicPub = schedule.BiblePublicationIsMusic ||
            (!string.IsNullOrWhiteSpace(schedule.BiblePublicationCode) && JwSourceHelper.MusicFlagPublicationCodes.Contains(schedule.BiblePublicationCode));
        if (!isMusicPub && string.IsNullOrWhiteSpace(schedule.BiblePublicationLanguageCode))
        {
            return false;
        }

        // For sectioned publications, section code is required.
        // For dramas (no section structure), section code is not required (null is valid).
        if (PublicationTypeHelper.HasSectionStructure(schedule.BiblePublicationCode))
        {
            var sectionCode = schedule.BiblePublicationSectionCode;
            return !string.IsNullOrWhiteSpace(sectionCode);
        }

        // For dramas, section code is not required
        return true;
    }

    public static BiblePublicationStateItem CreateBiblePublicationScheduleFromCurrent(ScheduleStateItem updatedCurrentSchedule)
    {
        if (string.IsNullOrWhiteSpace(updatedCurrentSchedule.BiblePublicationTrackCode))
        {
            throw new InvalidOperationException("BiblePublicationTrackCode must have a value");
        }

        var hasSectionStructure = PublicationTypeHelper.HasSectionStructure(updatedCurrentSchedule.BiblePublicationCode);

        // For sectioned publications, section code is required
        var sectionCode = updatedCurrentSchedule.BiblePublicationSectionCode;
        if (hasSectionStructure && string.IsNullOrWhiteSpace(sectionCode))
        {
            throw new InvalidOperationException("BiblePublicationSectionCode must have a value for publications with section structure");
        }

        return new BiblePublicationStateItem
        {
            Id = updatedCurrentSchedule.BiblePublicationScheduleId ?? 0,
            CategoryId = updatedCurrentSchedule.BiblePublicationCategoryId,
            CategoryName = updatedCurrentSchedule.BiblePublicationCategoryName,
            LanguageCode = updatedCurrentSchedule.BiblePublicationLanguageCode ?? string.Empty,
            PublicationCode = updatedCurrentSchedule.BiblePublicationCode ?? string.Empty,
            SectionCode = sectionCode,
            TrackCode = updatedCurrentSchedule.BiblePublicationTrackCode ?? string.Empty,
            FinishedDuration = updatedCurrentSchedule.BiblePublicationFinishedDuration ?? TimeSpan.Zero,
            AlarmScheduleId = updatedCurrentSchedule.Id,
            LanguageName = updatedCurrentSchedule.BiblePublicationLanguageName,
            LanguageDirection = updatedCurrentSchedule.BiblePublicationLanguageDirection,
            PublicationName = updatedCurrentSchedule.BiblePublicationName,
            SectionName = updatedCurrentSchedule.BiblePublicationSectionName,
            TrackTitle = updatedCurrentSchedule.BiblePublicationTrackTitle
        };
    }

    public static MusicStateItem? SyncMusicIfNeeded(UpdateScheduleFromViewModelAction action, ScheduleStateItem? updatedCurrentSchedule)
    {
        if (!action.MusicUpdated || updatedCurrentSchedule == null)
        {
            return null;
        }

        if (!HasValidMusicProperties(updatedCurrentSchedule))
        {
            return null;
        }

        var music = CreateMusicFromCurrent(updatedCurrentSchedule);
        Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Synced CurrentMusic from CurrentSchedule. LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}",
            music.LanguageCode ?? "(null)", music.PublicationCode);
        return music;
    }

    public static bool HasValidMusicProperties(ScheduleStateItem schedule)
    {
        return !string.IsNullOrWhiteSpace(schedule.MusicPublicationCode) &&
               !string.IsNullOrWhiteSpace(schedule.MusicTrackCode);
    }

    public static MusicStateItem CreateMusicFromCurrent(ScheduleStateItem updatedCurrentSchedule)
    {
        if (string.IsNullOrWhiteSpace(updatedCurrentSchedule.MusicTrackCode))
        {
            throw new InvalidOperationException("MusicTrackCode must have a value");
        }
        return new MusicStateItem
        {
            Id = updatedCurrentSchedule.MusicId ?? 0,
            PublicationCode = updatedCurrentSchedule.MusicPublicationCode ?? string.Empty,
            LanguageCode = updatedCurrentSchedule.MusicLanguageCode,
            SectionCode = updatedCurrentSchedule.MusicSectionCode,
            TrackCode = updatedCurrentSchedule.MusicTrackCode ?? string.Empty,
            Repeat = updatedCurrentSchedule.MusicRepeat ?? false,
            AlarmScheduleId = updatedCurrentSchedule.Id,
            LanguageName = updatedCurrentSchedule.MusicLanguageName,
            LanguageDirection = updatedCurrentSchedule.MusicLanguageDirection,
            PublicationName = updatedCurrentSchedule.MusicPublicationName,
            SectionName = updatedCurrentSchedule.MusicSectionName,
            TrackName = updatedCurrentSchedule.MusicTrackName
        };
    }

    /// <summary>
    /// Preserves basic schedule properties (DaysOfWeek, time, etc.) if action has invalid values but existing has valid values.
    /// This prevents cascade handlers or other updates from clearing essential schedule properties.
    /// </summary>
    private static void PreserveBasicScheduleProperties(ScheduleStateItem actionSchedule, ScheduleStateItem existingSchedule)
    {
        // Preserve DaysOfWeek if action has it as 0 but existing has a valid value
        if (actionSchedule.DaysOfWeek == 0 && existingSchedule.DaysOfWeek != 0)
        {
            Log.Warning("ScheduleStateSyncHelper: Preserving DaysOfWeek from existing schedule. Action had DaysOfWeek=0, existing has {ExistingDaysOfWeek}",
                existingSchedule.DaysOfWeek);
            actionSchedule.DaysOfWeek = existingSchedule.DaysOfWeek;
        }

        // Preserve time if action has invalid values but existing has valid values
        if (actionSchedule.Hour == 0 && actionSchedule.Minute == 0 && 
            (existingSchedule.Hour != 0 || existingSchedule.Minute != 0))
        {
            // Only preserve if action time is truly invalid (midnight) and existing is not
            // But be careful - midnight (0:00) is a valid time, so only preserve if both hour and minute are 0
            // and existing has a different time
            if (existingSchedule.Hour != 0 || existingSchedule.Minute != 0)
            {
                Log.Debug("ScheduleStateSyncHelper: Preserving time from existing schedule. Action had Hour={ActionHour}, Minute={ActionMinute}, existing has Hour={ExistingHour}, Minute={ExistingMinute}",
                    actionSchedule.Hour, actionSchedule.Minute, existingSchedule.Hour, existingSchedule.Minute);
                actionSchedule.Hour = existingSchedule.Hour;
                actionSchedule.Minute = existingSchedule.Minute;
                actionSchedule.Second = existingSchedule.Second;
            }
        }
    }
}

