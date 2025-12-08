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

    Task<KeyValuePair<BibleBook, BibleChapter>> GetNextBibleChapter(string languageCode, string publicationCode,
        int bookNumber, int chapter);

    Task<KeyValuePair<BibleBook, BibleChapter>> GetPreviousBibleChapter(string languageCode, string publicationCode,
        int bookNumber, int chapter);

    Task<KeyValuePair<int, BibleBook>>
        GetPreviousBibleBook(string languageCode, string publicationCode, int bookNumber);

    Task<KeyValuePair<int, BibleBook>> GetNextBibleBook(string languageCode, string publicationCode, int bookNumber);
    Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId);
}

