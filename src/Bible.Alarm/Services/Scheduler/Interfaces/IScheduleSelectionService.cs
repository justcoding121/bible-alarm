#nullable enable
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Services.Scheduler.Interfaces;

public interface IScheduleSelectionService : IDisposable
{
    /// <summary>
    /// Loads music for selection modal. For existing schedules, creates AlarmMusic from CurrentSchedule properties
    /// instead of querying AlarmDB (since all data is already in CurrentSchedule from page load).
    /// Only queries media index DB for track lists, publications, etc.
    /// </summary>
    AlarmMusic? LoadMusicForSelection(int scheduleId, bool isNewSchedule, AlarmMusic? currentMusic,
        MusicType? musicType, string? publicationCode, string? languageCode, int? trackNumber, bool? repeat);

    /// <summary>
    /// Loads Bible reading for selection modal. For existing schedules, creates BiblePublicationSchedule from CurrentSchedule properties
    /// instead of querying AlarmDB (since all data is already in CurrentSchedule from page load).
    /// Only queries media index DB for section lists, tracks, etc.
    /// </summary>
    BiblePublicationSchedule? LoadBiblePublicationForSelection(int scheduleId, bool isNewSchedule, BiblePublicationSchedule? currentBiblePublication,
        string? languageCode, string? publicationCode, int? sectionNumber, int? trackNumber, TimeSpan? finishedDuration);
}

