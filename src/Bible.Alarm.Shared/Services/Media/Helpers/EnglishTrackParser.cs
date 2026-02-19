#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
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
        string sectionCode,
        ApiUrl apiUrl)
    {
        var tracks = new List<BiblePublicationTrack>();
        
        // Parse tracks from files.E.MP3
        if (filesElement.TryGetProperty("E", out var englishFiles) &&
            englishFiles.TryGetProperty("MP3", out var mp3Files))
        {
            var trackNumber = 1;
            foreach (var trackFile in mp3Files.EnumerateArray())
            {
                var track = ParseTrackFromJson(trackFile, trackNumber, apiUrl, sectionCode, isIam: true);
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
        string sectionCode,
        ApiUrl apiUrl)
    {
        var tracks = new List<BiblePublicationTrack>();
        
        // Parse tracks from files.{languageCode}.MP3
        if (filesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) &&
            languageFiles.TryGetProperty("MP3", out var mp3Files))
        {
            var trackNumber = 1;
            foreach (var trackFile in mp3Files.EnumerateArray())
            {
                var track = ParseTrackFromJson(trackFile, trackNumber, apiUrl, sectionCode,
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

    private BiblePublicationTrack? ParseTrackFromJson(
        JsonElement trackFile,
        int trackNumber,
        ApiUrl apiUrl,
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

        // Skip audio descriptions
        if (title.Contains("audio descriptions", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Create track with URL params
        var trackUrlParams = new List<UrlParam>();

        if (isIam)
        {
            trackUrlParams.Add(new UrlParam
            {
                Key = "pub",
                Value = sectionCode,
                IsQueryParam = true,
                ApiUrl = apiUrl,
                ApiUrlId = apiUrl.Id
            });
            trackUrlParams.Add(new UrlParam
            {
                Key = "fileformat",
                Value = "mp3",
                IsQueryParam = true,
                ApiUrl = apiUrl,
                ApiUrlId = apiUrl.Id
            });
            trackUrlParams.Add(new UrlParam
            {
                Key = "track",
                Value = originalTrackCode.ToString(),
                IsQueryParam = true,
                ApiUrl = apiUrl,
                ApiUrlId = apiUrl.Id
            });
        }
        else if (isBible && normalizedPublicationCode != null && normalizedLanguageCode != null)
        {
            trackUrlParams.Add(new UrlParam
            {
                Key = "pub",
                Value = normalizedPublicationCode,
                IsQueryParam = true,
                ApiUrl = apiUrl,
                ApiUrlId = apiUrl.Id
            });
            trackUrlParams.Add(new UrlParam
            {
                Key = "booknum",
                Value = sectionCode,
                IsQueryParam = true,
                ApiUrl = apiUrl,
                ApiUrlId = apiUrl.Id
            });
            trackUrlParams.Add(new UrlParam
            {
                Key = "track",
                Value = originalTrackCode.ToString(),
                IsQueryParam = true,
                ApiUrl = apiUrl,
                ApiUrlId = apiUrl.Id
            });
            trackUrlParams.Add(new UrlParam
            {
                Key = "fileformat",
                Value = "mp3",
                IsQueryParam = true,
                ApiUrl = apiUrl,
                ApiUrlId = apiUrl.Id
            });
            trackUrlParams.Add(new UrlParam
            {
                Key = "alllangs",
                Value = "0",
                IsQueryParam = true,
                ApiUrl = apiUrl,
                ApiUrlId = apiUrl.Id
            });
            trackUrlParams.Add(new UrlParam
            {
                Key = "langwritten",
                Value = normalizedLanguageCode,
                IsQueryParam = true,
                ApiUrl = apiUrl,
                ApiUrlId = apiUrl.Id
            });
        }

        // TrackCode is the originalTrackCode from API (track param value), not the sequential trackNumber
        return new BiblePublicationTrack
        {
            TrackCode = originalTrackCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Title = title,
            UrlParams = trackUrlParams
        };
    }
}
