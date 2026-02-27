#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Helper class for parsing tracks from JSON responses for English content seeding.
/// </summary>
internal sealed class EnglishTrackParser
{
    private readonly ILogger logger;

    public EnglishTrackParser(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Parses iam (Kingdom Melodies) tracks from JSON response.
    /// </summary>
    public List<BiblePublicationTrack> ParseIamTracks(
        JsonElement filesElement,
        string sectionCode)
    {
        var tracks = new List<BiblePublicationTrack>();
        
        // Parse tracks from files.E.MP3
        if (filesElement.TryGetProperty("E", out var englishFiles) &&
            englishFiles.TryGetProperty("MP3", out var mp3Files))
        {
            var trackNumber = 1;
            foreach (var trackFile in mp3Files.EnumerateArray())
            {
                var track = ParseTrackFromJson(trackFile, trackNumber, sectionCode, isIam: true);
                if (track != null)
                {
                    tracks.Add(track);
                    trackNumber++;
                }
            }
        }

        return tracks;
    }

    /// <summary>
    /// Parses Bible publication tracks from JSON response.
    /// </summary>
    public List<BiblePublicationTrack> ParseBibleTracks(
        JsonElement filesElement,
        string normalizedLanguageCode,
        string normalizedPublicationCode,
        string sectionCode)
    {
        var tracks = new List<BiblePublicationTrack>();
        
        // Parse tracks from files.{languageCode}.MP3
        if (filesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) &&
            languageFiles.TryGetProperty("MP3", out var mp3Files))
        {
            var trackNumber = 1;
            foreach (var trackFile in mp3Files.EnumerateArray())
            {
                var track = ParseTrackFromJson(trackFile, trackNumber, sectionCode,
                    normalizedPublicationCode, normalizedLanguageCode, isBible: true);
                if (track != null)
                {
                    tracks.Add(track);
                    trackNumber++;
                }
            }
        }

        return tracks;
    }

    /// <summary>
    /// Parses tracks for non-Bible, non-iam sectioned publications (e.g., video sections).
    /// </summary>
    public List<BiblePublicationTrack> ParseGenericTracks(
        JsonElement filesElement,
        string normalizedLanguageCode,
        string fileFormat,
        string sectionCode)
    {
        var tracks = new List<BiblePublicationTrack>();

        if (filesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) &&
            languageFiles.TryGetProperty(fileFormat, out var formatFiles))
        {
            var trackNumber = 1;
            foreach (var trackFile in formatFiles.EnumerateArray())
            {
                var track = ParseTrackFromJson(trackFile, trackNumber, sectionCode);
                if (track != null)
                {
                    tracks.Add(track);
                    trackNumber++;
                }
            }
        }

        return tracks;
    }

    private BiblePublicationTrack? ParseTrackFromJson(
        JsonElement trackFile,
        int trackNumber,
        string sectionCode,
        string? normalizedPublicationCode = null,
        string? normalizedLanguageCode = null,
        bool isIam = false,
        bool isBible = false)
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
