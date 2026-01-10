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

    Task MoveToNextBibleTrack(int scheduleId);
    Task MoveToPreviousBibleTrack(int scheduleId);

    Task<KeyValuePair<BiblePublicationSection, BiblePublicationTrack>> GetNextBibleTrack(string languageCode, string publicationCode,
        int sectionNumber, int track);

    Task<KeyValuePair<BiblePublicationSection, BiblePublicationTrack>> GetPreviousBibleTrack(string languageCode, string publicationCode,
        int sectionNumber, int track);

    Task<KeyValuePair<int, BiblePublicationSection>>
        GetPreviousBiblePublicationSection(string languageCode, string publicationCode, int sectionNumber);

    Task<KeyValuePair<int, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode, string publicationCode, int sectionNumber);
    Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId);
}

