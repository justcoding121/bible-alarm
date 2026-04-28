#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Parsing tracks from JSON responses for English content seeding.
/// </summary>
internal static class EnglishTrackParser
{
    /// <summary>
    /// Parses iam (Kingdom Melodies) tracks from JSON response.
    /// </summary>
    public static List<BiblePublicationTrack> ParseIamTracks(JsonElement filesElement)
    {
        var tracks = new List<BiblePublicationTrack>();
        
        // Parse tracks from files.E.MP3
        if (filesElement.TryGetProperty("E", out var englishFiles) &&
            englishFiles.TryGetProperty("MP3", out var mp3Files))
        {
            foreach (var trackFile in mp3Files.EnumerateArray())
            {
                var track = ParseTrackFromJson(trackFile, "E");
                if (track != null)
                {
                    tracks.Add(track);
                }
            }
        }

        return tracks;
    }

    /// <summary>
    /// Parses Bible publication tracks from JSON response.
    /// </summary>
    public static List<BiblePublicationTrack> ParseBibleTracks(
        JsonElement filesElement,
        string normalizedLanguageCode)
    {
        var tracks = new List<BiblePublicationTrack>();
        
        // Parse tracks from files.{languageCode}.MP3
        if (filesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) &&
            languageFiles.TryGetProperty("MP3", out var mp3Files))
        {
            foreach (var trackFile in mp3Files.EnumerateArray())
            {
                var track = ParseTrackFromJson(trackFile, normalizedLanguageCode);
                if (track != null)
                {
                    tracks.Add(track);
                }
            }
        }

        return tracks;
    }

    /// <summary>
    /// Parses tracks for non-Bible, non-iam sectioned publications (e.g., video sections).
    /// </summary>
    public static List<BiblePublicationTrack> ParseGenericTracks(
        JsonElement filesElement,
        string normalizedLanguageCode,
        string fileFormat)
    {
        var tracks = new List<BiblePublicationTrack>();

        if (filesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) &&
            languageFiles.TryGetProperty(fileFormat, out var formatFiles))
        {
            foreach (var trackFile in formatFiles.EnumerateArray())
            {
                var track = ParseTrackFromJson(trackFile, normalizedLanguageCode);
                if (track != null)
                {
                    tracks.Add(track);
                }
            }
        }

        return tracks;
    }

    private static BiblePublicationTrack? ParseTrackFromJson(
        JsonElement trackFile,
        string? normalizedLanguageCode = null)
    {
        if (!trackFile.TryGetProperty("file", out var fileElement) ||
            !fileElement.TryGetProperty("url", out var urlElement))
        {
            return null;
        }

        var url = urlElement.GetString();
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        // Get track number from API (original track number within the disc/book)
        int originalTrackCode = 0;
        if (trackFile.TryGetProperty("track", out var trackElement))
        {
            originalTrackCode = trackElement.GetInt32();
        }

        if (originalTrackCode == 0)
        {
            return null;
        }

        // Get title
        string title = "Unknown";
        if (trackFile.TryGetProperty("title", out var titleElement))
        {
            var rawTitle = titleElement.GetString();
            title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
        }

        if (AudioDescriptionTitlePhrases.ContainsAudioDescriptionPhrase(normalizedLanguageCode, title))
        {
            return null;
        }

        return new BiblePublicationTrack
        {
            TrackCode = originalTrackCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Title = title,
            TrackUrl = new TrackUrl { Url = url }
        };
    }
}
