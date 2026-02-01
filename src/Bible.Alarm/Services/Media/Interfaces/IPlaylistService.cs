#nullable enable
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

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

    Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetNextBiblePublicationTrack(string languageCode, string publicationCode,
        string? sectionCode, int track);

    Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetPreviousBiblePublicationTrack(string languageCode, string publicationCode,
        string? sectionCode, int track);

    Task<KeyValuePair<string, BiblePublicationSection>>
        GetPreviousBiblePublicationSection(string languageCode, string publicationCode, string sectionCode);

    Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode, string publicationCode, string sectionCode);
    Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId);

    /// <summary>
    /// Resolves the next track to play based on the currently playing track metadata.
    /// Used for indefinite playback and dynamic playlist extension.
    /// </summary>
    Task<PlayItem> GetNextPlayItemAsync(TrackMetadata currentTrackMetadata);

    /// <summary>
    /// Resolves the previous track to play based on the currently playing track metadata.
    /// </summary>
    Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata);
}

