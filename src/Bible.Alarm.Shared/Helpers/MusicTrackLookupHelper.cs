#nullable enable

using System.Collections.Generic;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Lookup of MusicTrack by track code (string). Dictionaries are keyed by index (int); matching uses CodeComparisonHelper so numeric and non-numeric codes compare correctly.
/// </summary>
public static class MusicTrackLookupHelper
{
    /// <summary>
    /// Tries to get a track by its code (string). Matches by TrackCode using CodeComparisonHelper.
    /// </summary>
    public static bool TryGetByCode(
        IReadOnlyDictionary<int, MusicTrack> tracks,
        string? trackCode,
        out (int Key, MusicTrack Track) result)
    {
        result = default;
        if (tracks == null || tracks.Count == 0 || string.IsNullOrWhiteSpace(trackCode))
            return false;

        foreach (var kvp in tracks)
        {
            if (CodeComparisonHelper.Equals(kvp.Value.TrackCode, trackCode))
            {
                result = (kvp.Key, kvp.Value);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the int key for the track with the given code, or null if not found.
    /// </summary>
    public static int? GetKeyByCode(IReadOnlyDictionary<int, MusicTrack> tracks, string? trackCode)
    {
        if (TryGetByCode(tracks, trackCode, out var pair))
            return pair.Key;
        return null;
    }
}
