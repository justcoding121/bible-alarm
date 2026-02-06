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
    /// Prefers URL param "track", then "pub" (drama), then Number as string.
    /// </summary>
    public static string GetFromTrack(BiblePublicationTrack track)
    {
        var trackParam = track.UrlParams?.FirstOrDefault(p => p.Key == "track");
        if (!string.IsNullOrEmpty(trackParam?.Value))
        {
            return trackParam.Value;
        }
        var pubParam = track.UrlParams?.FirstOrDefault(p => p.Key == "pub");
        if (!string.IsNullOrEmpty(pubParam?.Value))
        {
            return pubParam.Value;
        }
        return track.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
