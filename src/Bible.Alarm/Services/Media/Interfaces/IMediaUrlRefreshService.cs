#nullable enable
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaUrlRefreshService
{
    Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata);

    Task<string?> GetBibleChapterUrl(string languageCode, string pubCode, int bookNumber, int chapter, string lookUpPath);

    Task<string?> GetMusicTrackUrl(string languageCode, string lookUpPath);
}

