#nullable enable
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IPlaylistService : IDisposable
{
    Task MarkTrackAsPlayed(TrackMetadata trackMetadata);
    Task MarkTrackAsFinished(TrackMetadata trackMetadata);
    Task<PlayItem> NextTrack(int scheduleId);
    Task<PlayItem?> NextBiblePublicationTrack(int scheduleId);
    Task<List<PlayItem>> NextTracks(int scheduleId);
    Task SaveLastPlayed(int currentScheduleId);

    Task<int> GetRelevantScheduleToPlay();

    Task MoveToNextBiblePublicationTrack(int scheduleId);
    Task MoveToPreviousBiblePublicationTrack(int scheduleId);

    Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetNextBiblePublicationTrack(string languageCode, string publicationCode,
        string? sectionCode, string trackCode);

    Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetPreviousBiblePublicationTrack(string languageCode, string publicationCode,
        string? sectionCode, string trackCode);

    Task<KeyValuePair<string, BiblePublicationSection>>
        GetPreviousBiblePublicationSection(string languageCode, string publicationCode, string sectionCode);

    Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode, string publicationCode, string sectionCode);
    Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId);

    /// <summary>
    /// Resolves the next track to play based on the currently playing track metadata.
    /// Used for indefinite playback and dynamic playlist extension.
    /// </summary>
    /// <param name="sectionFetchProgress">Optional progress reporter for ad-hoc API section fetch (0-100%).</param>
    Task<PlayItem> GetNextPlayItemAsync(TrackMetadata currentTrackMetadata, IFetchProgress? sectionFetchProgress = null);

    /// <summary>
    /// Resolves the previous track to play based on the currently playing track metadata.
    /// </summary>
    /// <param name="sectionFetchProgress">Optional progress reporter for ad-hoc API section fetch (0-100%).</param>
    Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata, IFetchProgress? sectionFetchProgress = null);
}

