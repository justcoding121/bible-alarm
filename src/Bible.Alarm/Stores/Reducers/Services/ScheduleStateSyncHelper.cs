#nullable enable
using Bible;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Stores.Reducers.Services;

/// <summary>
/// Handles syncing of CurrentSchedule, CurrentMusic, and CurrentBibleReadingSchedule.
/// Separated from ApplicationReducer for better modularity.
/// </summary>
public static class ScheduleStateSyncHelper
{
    public static ScheduleStateItem? UpdateCurrentScheduleIfMatches(ApplicationState state, ScheduleStateItem actionSchedule)
    {
        if (state.CurrentSchedule?.Id == actionSchedule.Id)
        {
            Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Updating CurrentSchedule. New LanguageName: {LanguageName}, PublicationName: {PublicationName}",
                actionSchedule.BibleReadingLanguageName ?? "null",
                actionSchedule.BibleReadingPublicationName ?? "null");
            return actionSchedule.DeepClone();
        }

        Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - CurrentSchedule ID ({CurrentScheduleId}) doesn't match action Schedule ID ({ActionScheduleId}), not updating CurrentSchedule",
            state.CurrentSchedule?.Id ?? 0, actionSchedule.Id);
        return state.CurrentSchedule;
    }

    public static BibleReadingStateItem? SyncBibleReadingScheduleIfNeeded(UpdateScheduleFromViewModelAction action, ScheduleStateItem? updatedCurrentSchedule)
    {
        if (!action.BibleReadingUpdated || updatedCurrentSchedule == null)
        {
            return null;
        }

        if (!HasValidBibleReadingProperties(updatedCurrentSchedule))
        {
            return null;
        }

        var bibleReadingSchedule = CreateBibleReadingScheduleFromCurrent(updatedCurrentSchedule);
        Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Synced CurrentBibleReadingSchedule from CurrentSchedule. LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}",
            bibleReadingSchedule.LanguageCode, bibleReadingSchedule.PublicationCode);
        return bibleReadingSchedule;
    }

    public static bool HasValidBibleReadingProperties(ScheduleStateItem schedule)
    {
        // Basic required properties for all content types
        if (string.IsNullOrWhiteSpace(schedule.BibleReadingLanguageCode) ||
            string.IsNullOrWhiteSpace(schedule.BibleReadingPublicationCode) ||
            !schedule.BibleReadingTrackNumber.HasValue ||
            schedule.BibleReadingTrackNumber.Value <= 0)
        {
            return false;
        }

        // For traditional Bible reading (has section structure), SectionNumber is required
        // For dramas (no section structure), SectionNumber is not required (null is valid)
        if (PublicationTypeHelper.HasSectionStructure(schedule.BibleReadingPublicationCode))
        {
            return schedule.BibleReadingSectionNumber.HasValue &&
                   schedule.BibleReadingSectionNumber.Value > 0;
        }

        // For dramas, SectionNumber is not required
        return true;
    }

    public static BibleReadingStateItem CreateBibleReadingScheduleFromCurrent(ScheduleStateItem updatedCurrentSchedule)
    {
        if (!updatedCurrentSchedule.BibleReadingTrackNumber.HasValue)
        {
            throw new InvalidOperationException("BibleReadingTrackNumber must have a value");
        }

        var hasSectionStructure = PublicationTypeHelper.HasSectionStructure(updatedCurrentSchedule.BibleReadingPublicationCode);

        // For traditional Bible reading (has section structure), SectionNumber is required
        if (hasSectionStructure && !updatedCurrentSchedule.BibleReadingSectionNumber.HasValue)
        {
            throw new InvalidOperationException("BibleReadingSectionNumber must have a value for publications with section structure");
        }

        return new BibleReadingStateItem
        {
            Id = updatedCurrentSchedule.BibleReadingScheduleId ?? 0,
            LanguageCode = updatedCurrentSchedule.BibleReadingLanguageCode ?? string.Empty,
            PublicationCode = updatedCurrentSchedule.BibleReadingPublicationCode ?? string.Empty,
            SectionNumber = updatedCurrentSchedule.BibleReadingSectionNumber,
            TrackNumber = updatedCurrentSchedule.BibleReadingTrackNumber.Value,
            FinishedDuration = updatedCurrentSchedule.BibleReadingFinishedDuration ?? TimeSpan.Zero,
            AlarmScheduleId = updatedCurrentSchedule.Id,
            TranslationName = updatedCurrentSchedule.BibleReadingPublicationName ?? string.Empty
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
        Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Synced CurrentMusic from CurrentSchedule. MusicType: {MusicType}, LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}",
            music.MusicType, music.LanguageCode ?? "null", music.PublicationCode);
        return music;
    }

    public static bool HasValidMusicProperties(ScheduleStateItem schedule)
    {
        return schedule.MusicType.HasValue &&
               !string.IsNullOrWhiteSpace(schedule.MusicPublicationCode) &&
               schedule.MusicTrackNumber.HasValue &&
               schedule.MusicTrackNumber.Value > 0;
    }

    public static MusicStateItem CreateMusicFromCurrent(ScheduleStateItem updatedCurrentSchedule)
    {
        if (!updatedCurrentSchedule.MusicType.HasValue || !updatedCurrentSchedule.MusicTrackNumber.HasValue)
        {
            throw new InvalidOperationException("MusicType and MusicTrackNumber must have values");
        }
        return new MusicStateItem
        {
            Id = updatedCurrentSchedule.MusicId ?? 0,
            MusicType = updatedCurrentSchedule.MusicType.Value,
            PublicationCode = updatedCurrentSchedule.MusicPublicationCode ?? string.Empty,
            LanguageCode = updatedCurrentSchedule.MusicLanguageCode ?? string.Empty,
            TrackNumber = updatedCurrentSchedule.MusicTrackNumber.Value,
            Repeat = updatedCurrentSchedule.MusicRepeat ?? false,
            AlarmScheduleId = updatedCurrentSchedule.Id,
            LanguageName = updatedCurrentSchedule.MusicLanguageName,
            PublicationName = updatedCurrentSchedule.MusicPublicationName,
            TrackName = updatedCurrentSchedule.MusicTrackName
        };
    }
}

