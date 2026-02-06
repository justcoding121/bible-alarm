#nullable enable
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Helpers for resolving track code (string) from track entities.
/// Track code is the API identifier: "track" param value, or "pub" for drama, or Number as string.
/// </summary>
public static class TrackCodeHelper
{
    /// <summary>
    /// Gets the stable track code from a Bible publication track (for schedule persistence and lookup).
    /// TrackCode is now the primary property, so this just returns it directly.
    /// </summary>
    public static string GetFromTrack(BiblePublicationTrack track)
    {
        return track.TrackCode;
    }
}
