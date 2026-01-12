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
            Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - Updating CurrentSchedule. New LanguageName: {LanguageName}, PublicationName: {PublicationName}",
                actionSchedule.BiblePublicationLanguageName ?? "null",
                actionSchedule.BiblePublicationName ?? "null");
            return actionSchedule.DeepClone();
        }

        Log.Debug("ApplicationReducer: OnUpdateScheduleFromViewModel - CurrentSchedule ID ({CurrentScheduleId}) doesn't match action Schedule ID ({ActionScheduleId}), not updating CurrentSchedule",
            state.CurrentSchedule?.Id ?? 0, actionSchedule.Id);
        return state.CurrentSchedule;
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
        // Basic required properties for all content types
        if (string.IsNullOrWhiteSpace(schedule.BiblePublicationLanguageCode) ||
            string.IsNullOrWhiteSpace(schedule.BiblePublicationCode) ||
            !schedule.BiblePublicationTrackNumber.HasValue ||
            schedule.BiblePublicationTrackNumber.Value <= 0)
        {
            return false;
        }

        // For traditional Bible reading (has section structure), SectionNumber is required
        // For dramas (no section structure), SectionNumber is not required (null is valid)
        if (PublicationTypeHelper.HasSectionStructure(schedule.BiblePublicationCode))
        {
            return schedule.BiblePublicationSectionNumber.HasValue &&
                   schedule.BiblePublicationSectionNumber.Value > 0;
        }

        // For dramas, SectionNumber is not required
        return true;
    }

    public static BiblePublicationStateItem CreateBiblePublicationScheduleFromCurrent(ScheduleStateItem updatedCurrentSchedule)
    {
        if (!updatedCurrentSchedule.BiblePublicationTrackNumber.HasValue)
        {
            throw new InvalidOperationException("BiblePublicationTrackNumber must have a value");
        }

        var hasSectionStructure = PublicationTypeHelper.HasSectionStructure(updatedCurrentSchedule.BiblePublicationCode);

        // For traditional Bible reading (has section structure), SectionNumber is required
        if (hasSectionStructure && !updatedCurrentSchedule.BiblePublicationSectionNumber.HasValue)
        {
            throw new InvalidOperationException("BiblePublicationSectionNumber must have a value for publications with section structure");
        }

        return new BiblePublicationStateItem
        {
            Id = updatedCurrentSchedule.BiblePublicationScheduleId ?? 0,
            LanguageCode = updatedCurrentSchedule.BiblePublicationLanguageCode ?? string.Empty,
            PublicationCode = updatedCurrentSchedule.BiblePublicationCode ?? string.Empty,
            SectionNumber = updatedCurrentSchedule.BiblePublicationSectionNumber,
            TrackNumber = updatedCurrentSchedule.BiblePublicationTrackNumber.Value,
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

