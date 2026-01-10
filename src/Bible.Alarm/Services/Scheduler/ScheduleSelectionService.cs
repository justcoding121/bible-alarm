#nullable enable
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;

namespace Bible.Alarm.Services.Scheduler;

public sealed class ScheduleSelectionService
    : IScheduleSelectionService, IDisposable
{
    private bool isDisposed;

    /// <summary>
    /// Loads music for selection modal. Creates AlarmMusic from CurrentSchedule properties
    /// instead of querying AlarmDB (since all data is already in CurrentSchedule from page load).
    /// Only queries media index DB for track lists, publications, etc.
    /// </summary>
    public AlarmMusic? LoadMusicForSelection(int scheduleId, bool isNewSchedule, AlarmMusic? currentMusic,
        MusicType? musicType, string? publicationCode, string? languageCode, int? trackNumber, bool? repeat)
    {
        // Create AlarmMusic from CurrentSchedule properties if we have the required data
        // This works for both new and existing schedules - CurrentSchedule is the source of truth
        // For new schedules, CurrentSchedule has the music data from when it was created/updated
        // For existing schedules, CurrentSchedule has the music data loaded from AlarmDB on page load
        if (musicType.HasValue && !string.IsNullOrWhiteSpace(publicationCode) && trackNumber.HasValue)
        {
            return new AlarmMusic
            {
                Id = 0, // Will be set when saved
                MusicType = musicType.Value,
                PublicationCode = publicationCode,
                LanguageCode = languageCode,
                TrackNumber = trackNumber.Value,
                Repeat = repeat ?? false,
                AlarmScheduleId = scheduleId
            };
        }

        // Fallback to currentMusic if CurrentSchedule doesn't have required properties
        return currentMusic;
    }

    /// <summary>
    /// Loads Bible reading for selection modal. For existing schedules, creates BibleReadingSchedule from CurrentSchedule properties
    /// instead of querying AlarmDB (since all data is already in CurrentSchedule from page load).
    /// Only queries media index DB for section lists, tracks, etc.
    /// </summary>
    public BibleReadingSchedule? LoadBibleReadingForSelection(int scheduleId, bool isNewSchedule, BibleReadingSchedule? currentBibleReading,
        string? languageCode, string? publicationCode, int? sectionNumber, int? trackNumber, TimeSpan? finishedDuration)
    {
        // For new schedules, return currentBibleReading (which may be null)
        if (isNewSchedule)
        {
            return currentBibleReading;
        }

        // For existing schedules, create BibleReadingSchedule from CurrentSchedule properties
        // All data is already loaded from AlarmDB when the schedule page opened
        // No need to query AlarmDB again - only media index DB queries are needed for selection lists
        if (!string.IsNullOrWhiteSpace(languageCode) && 
            !string.IsNullOrWhiteSpace(publicationCode) && 
            sectionNumber.HasValue && 
            trackNumber.HasValue)
        {
            return new BibleReadingSchedule
            {
                Id = 0, // Will be set when saved
                LanguageCode = languageCode,
                PublicationCode = publicationCode,
                SectionNumber = sectionNumber.Value,
                TrackNumber = trackNumber.Value,
                FinishedDuration = finishedDuration ?? TimeSpan.Zero,
                AlarmScheduleId = scheduleId
            };
        }

        // Fallback to currentBibleReading if CurrentSchedule doesn't have required properties
        return currentBibleReading;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        // No resources to dispose - all methods are synchronous now
    }
}

