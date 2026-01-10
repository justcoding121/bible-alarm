#nullable enable
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaUrlRefreshService
{
    Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata);

    Task<string?> GetBiblePublicationTrackUrl(string languageCode, string pubCode, int sectionNumber, int track, string lookUpPath);

    Task<string?> GetMusicTrackUrl(string languageCode, string lookUpPath);
}

