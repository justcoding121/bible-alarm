using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IPlaylistService : IDisposable
{
    Task MarkTrackAsPlayed(TrackMetadata trackMetadata);
    Task MarkTrackAsFinished(TrackMetadata trackMetadata);
    Task<PlayItem> NextTrack(int scheduleId);
    Task<List<PlayItem>> NextTracks(int scheduleId);
    Task SaveLastPlayed(int currentScheduleId);

    Task<int> GetRelevantScheduleToPlay();

    Task MoveToNextBibleChapter(int scheduleId);
    Task MoveToPreviousBibleChapter(int scheduleId);

    Task<KeyValuePair<BibleSection, BiblePublicationChapter>> GetNextBibleChapter(string languageCode, string publicationCode,
        int sectionNumber, int chapter);

    Task<KeyValuePair<BibleSection, BiblePublicationChapter>> GetPreviousBibleChapter(string languageCode, string publicationCode,
        int sectionNumber, int chapter);

    Task<KeyValuePair<int, BibleSection>>
        GetPreviousBibleSection(string languageCode, string publicationCode, int sectionNumber);

    Task<KeyValuePair<int, BibleSection>> GetNextBibleSection(string languageCode, string publicationCode, int sectionNumber);
    Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId);
}

