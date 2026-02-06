#nullable enable
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaUrlRefreshService
{
    Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata);

    Task<string?> GetBiblePublicationTrackUrl(string languageCode, string pubCode, string trackCode, string lookUpPath);

    Task<string?> GetMusicTrackUrl(string languageCode, string lookUpPath);

    Task<string?> GetDramaTrackUrl(string categoryKey, string languageCode, string trackCode, string? naturalKey = null);
}

