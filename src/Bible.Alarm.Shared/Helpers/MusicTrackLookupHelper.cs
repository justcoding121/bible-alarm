#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Lookup of MusicTrack by code when the dictionary is keyed by index (int).
/// Supports both numeric track codes (lookup by key) and non-numeric codes (e.g. "jwb-201708") via TrackCode.
/// </summary>
public static class MusicTrackLookupHelper
{
    /// <summary>
    /// Tries to get a track by its code (string). Tries numeric key first, then matches by TrackCode.
    /// </summary>
    public static bool TryGetByCode(
        IReadOnlyDictionary<int, MusicTrack> tracks,
        string? trackCode,
        out (int Key, MusicTrack Track) result)
    {
        result = default;
        if (tracks == null || tracks.Count == 0 || string.IsNullOrWhiteSpace(trackCode))
            return false;

        if (int.TryParse(trackCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var key) &&
            tracks.TryGetValue(key, out var byKey))
        {
            result = (key, byKey);
            return true;
        }

        foreach (var kvp in tracks)
        {
            if (string.Equals(kvp.Value.TrackCode, trackCode, StringComparison.OrdinalIgnoreCase))
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
