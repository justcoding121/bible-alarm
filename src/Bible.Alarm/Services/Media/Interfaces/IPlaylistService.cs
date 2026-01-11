using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IPlaylistService : IDisposable
{
    Task MarkTrackAsPlayed(TrackMetadata trackMetadata);
    Task MarkTrackAsFinished(TrackMetadata trackMetadata);
    Task<PlayItem> NextTrack(int scheduleId);
    Task<List<PlayItem>> NextTracks(int scheduleId);
    Task SaveLastPlayed(int currentScheduleId);

    Task<int> GetRelevantScheduleToPlay();

    Task MoveToNextBiblePublicationTrack(int scheduleId);
    Task MoveToPreviousBiblePublicationTrack(int scheduleId);

    Task<KeyValuePair<BiblePublicationSection, BiblePublicationTrack>> GetNextBiblePublicationTrack(string languageCode, string publicationCode,
        int sectionNumber, int track);

    Task<KeyValuePair<BiblePublicationSection, BiblePublicationTrack>> GetPreviousBiblePublicationTrack(string languageCode, string publicationCode,
        int sectionNumber, int track);

    Task<KeyValuePair<int, BiblePublicationSection>>
        GetPreviousBiblePublicationSection(string languageCode, string publicationCode, int sectionNumber);

    Task<KeyValuePair<int, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode, string publicationCode, int sectionNumber);
    Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId);
}

