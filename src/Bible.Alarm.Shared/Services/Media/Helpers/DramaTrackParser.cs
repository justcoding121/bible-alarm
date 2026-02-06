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
/// Helper class for parsing drama tracks from JSON responses.
/// </summary>
internal sealed class DramaTrackParser
{
    private readonly ILogger logger;

    public DramaTrackParser(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public List<BiblePublicationTrack> ParseTracksFromJson(
        JsonElement sectionFilesElement,
        string normalizedLanguageCode,
        string sectionCode,
        BaseUrl baseUrl,
        int startTrackNumber,
        out int nextTrackNumber)
    {
        var tracks = new List<BiblePublicationTrack>();
        nextTrackNumber = startTrackNumber;

        if (!sectionFilesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) ||
            !languageFiles.TryGetProperty("MP3", out var mp3Files))
        {
            return tracks;
        }

        foreach (var trackFile in mp3Files.EnumerateArray())
        {
            var track = ParseSingleTrack(trackFile, nextTrackNumber, sectionCode, baseUrl, normalizedLanguageCode);
            if (track != null)
            {
                tracks.Add(track);
                nextTrackNumber++;
            }
        }

        return tracks;
    }

    private BiblePublicationTrack? ParseSingleTrack(
        JsonElement trackFile,
        int trackNumber,
        string sectionCode,
        BaseUrl baseUrl,
        string normalizedLanguageCode)
    {
        // Handle both cases: file can be a string (direct URL) or an object with a "url" property
        if (!trackFile.TryGetProperty("file", out var fileElement))
        {
            return null;
        }

        string? url = null;
        if (fileElement.ValueKind == JsonValueKind.String)
        {
            url = fileElement.GetString();
        }
        else if (fileElement.ValueKind == JsonValueKind.Object && fileElement.TryGetProperty("url", out var urlElement))
        {
            url = urlElement.GetString();
        }

        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        // Get title - handle both string and object formats
        string title = "Unknown";
        if (trackFile.TryGetProperty("title", out var titleElement))
        {
            if (titleElement.ValueKind == JsonValueKind.String)
            {
                var rawTitle = titleElement.GetString();
                title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
            }
            else if (titleElement.ValueKind == JsonValueKind.Object && titleElement.TryGetProperty("text", out var titleTextElement))
            {
                var rawTitle = titleTextElement.GetString();
                title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
            }
        }

        // Skip audio descriptions
        if (title.Contains("audio descriptions", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Create track with URL params.
        // GETPUBMEDIALINKS for drama sections returns a single file per section (pub=sectionCode); no track param.
        var trackUrlParams = new List<UrlParam>
        {
            new UrlParam
            {
                Key = "pub",
                Value = sectionCode,
                IsQueryParam = true,
                BaseUrl = baseUrl,
                BaseUrlId = baseUrl.Id
            },
            new UrlParam
            {
                Key = "fileformat",
                Value = "mp3",
                IsQueryParam = true,
                BaseUrl = baseUrl,
                BaseUrlId = baseUrl.Id
            },
            new UrlParam
            {
                Key = "alllangs",
                Value = "0",
                IsQueryParam = true,
                BaseUrl = baseUrl,
                BaseUrlId = baseUrl.Id
            },
            new UrlParam
            {
                Key = "langwritten",
                Value = normalizedLanguageCode,
                IsQueryParam = true,
                BaseUrl = baseUrl,
                BaseUrlId = baseUrl.Id
            }
        };

        return new BiblePublicationTrack
        {
            Number = trackNumber,
            Title = title,
            UrlParams = trackUrlParams
        };
    }
}
