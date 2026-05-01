#nullable enable
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Services.Scheduler;

public sealed partial class ScheduleSelectionService
    : IScheduleSelectionService
{
    private bool isDisposed;

    /// <summary>
    /// Loads music for selection modal. Creates AlarmMusic from CurrentSchedule properties
    /// instead of querying AlarmDB (since all data is already in CurrentSchedule from page load).
    /// Only queries media index DB for track lists, publications, etc.
    /// </summary>
    public AlarmMusic? LoadMusicForSelection(LoadMusicForSelectionArgs args)
    {
        // Create AlarmMusic from CurrentSchedule properties if we have the required data
        // This works for both new and existing schedules - CurrentSchedule is the source of truth
        // For new schedules, CurrentSchedule has the music data from when it was created/updated
        // For existing schedules, CurrentSchedule has the music data loaded from AlarmDB on page load
        if (!string.IsNullOrWhiteSpace(args.PublicationCode) && !string.IsNullOrWhiteSpace(args.TrackCode))
        {
            return new AlarmMusic
            {
                Id = 0, // Will be set when saved
                PublicationCode = args.PublicationCode,
                LanguageCode = args.LanguageCode,
                TrackCode = args.TrackCode,
                Repeat = args.Repeat ?? false,
                AlarmScheduleId = args.ScheduleId
            };
        }

        // Fallback to currentMusic if CurrentSchedule doesn't have required properties
        return args.CurrentMusic;
    }

    /// <summary>
    /// Loads Bible reading for selection modal. For existing schedules, creates BiblePublicationSchedule from CurrentSchedule properties
    /// instead of querying AlarmDB (since all data is already in CurrentSchedule from page load).
    /// Only queries media index DB for section lists, tracks, etc.
    /// </summary>
    public BiblePublicationSchedule? LoadBiblePublicationForSelection(LoadBiblePublicationForSelectionArgs args)
    {
        // For new schedules, return currentBiblePublication (which may be null)
        if (args.IsNewSchedule)
        {
            return args.CurrentBiblePublication;
        }

        var codes = args.Codes;

        // For existing schedules, create BiblePublicationSchedule from CurrentSchedule properties
        // All data is already loaded from AlarmDB when the schedule page opened
        // No need to query AlarmDB again - only media index DB queries are needed for selection lists
        if (!string.IsNullOrWhiteSpace(codes.PublicationCode) &&
            !string.IsNullOrWhiteSpace(codes.TrackCode))
        {
            return new BiblePublicationSchedule
            {
                Id = 0, // Will be set when saved
                // For publications without language, we persist empty string (DB requires a value).
                LanguageCode = codes.LanguageCode ?? string.Empty,
                PublicationCode = codes.PublicationCode,
                SectionCode = Bible.Alarm.Shared.Helpers.SectionCodeHelper.Normalize(codes.SectionCode),
                TrackCode = codes.TrackCode,
                FinishedDuration = args.FinishedDuration ?? TimeSpan.Zero,
                AlarmScheduleId = args.ScheduleId
            };
        }

        // Fallback to currentBiblePublication if CurrentSchedule doesn't have required properties
        return args.CurrentBiblePublication;
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

