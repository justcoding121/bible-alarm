using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;

namespace Bible.Alarm.Common.Interfaces.Media;

public interface IPlaylistService : IDisposable
{
    Task MarkTrackAsPlayed(NotificationDetail trackDetail);
    Task MarkTrackAsFinished(NotificationDetail trackDetail);
    Task<PlayItem> NextTrack(int scheduleId);
    Task<List<PlayItem>> NextTracks(int scheduleId);
    Task SaveLastPlayed(long currentScheduleId);

    Task<long> GetRelavantScheduleToPlay();

    Task MoveToNextBibleChapter(int scheduleId);
    Task MoveToPreviousBibleChapter(int scheduleId);

    Task<KeyValuePair<BibleBook, BibleChapter>> GetNextBibleChapter(string languageCode, string publicationCode,
        int bookNumber, int chapter);

    Task<KeyValuePair<BibleBook, BibleChapter>> GetPreviousBibleChapter(string languageCode, string publicationCode,
        int bookNumber, int chapter);

    Task<KeyValuePair<int, BibleBook>>
        GetPreviousBibleBook(string languageCode, string publicationCode, int bookNumber);

    Task<KeyValuePair<int, BibleBook>> GetNextBibleBook(string languageCode, string publicationCode, int bookNumber);
}