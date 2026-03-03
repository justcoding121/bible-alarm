#nullable enable
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IDisplayMetadataService
{
    Task<MetaData> GetDisplayMetadataAsync(AudioPlayerTrack track);

    /// <summary>
    /// Returns title, artist, album from DB/catalog only. Skips remote (HTTPS) artwork and
    /// remote metadata fallback so playback can start immediately on slow networks.
    /// </summary>
    Task<MetaData> GetCoreDisplayMetadataAsync(AudioPlayerTrack track);
}

