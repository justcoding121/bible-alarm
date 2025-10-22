using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;

namespace Bible.Alarm.Contracts.Media;

public interface IPlaylistService : IDisposable
{
    Task MarkTrackAsPlayed(NotificationDetail trackDetail);
    Task MarkTrackAsFinished(NotificationDetail trackDetail);
    Task<PlayItem> NextTrack(long scheduleId);
    Task<List<PlayItem>> NextTracks(long scheduleId);
    Task SaveLastPlayed(long currentScheduleId);

    Task<long> GetRelavantScheduleToPlay();

    Task MoveToNextBibleChapter(long scheduleId);
    Task MoveToPreviousBibleChapter(long scheduleId);

    Task<KeyValuePair<BibleBook, BibleChapter>> GetNextBibleChapter(string languageCode, string publicationCode,
        int bookNumber, int chapter);

    Task<KeyValuePair<BibleBook, BibleChapter>> GetPreviousBibleChapter(string languageCode, string publicationCode,
        int bookNumber, int chapter);

    Task<KeyValuePair<int, BibleBook>>
        GetPreviousBibleBook(string languageCode, string publicationCode, int bookNumber);

    Task<KeyValuePair<int, BibleBook>> GetNextBibleBook(string languageCode, string publicationCode, int bookNumber);
}