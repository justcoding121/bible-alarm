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
        bool isVideo = false)
    {
        var tracks = new List<BiblePublicationTrack>();
        var formatKey = isVideo ? "MP4" : "MP3";

        if (!sectionFilesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) ||
            !languageFiles.TryGetProperty(formatKey, out var formatFiles))
        {
            return tracks;
        }

        foreach (var trackFile in formatFiles.EnumerateArray())
        {
            var track = ParseSingleTrack(trackFile, sectionCode, normalizedLanguageCode, isVideo);
            if (track != null)
            {
                tracks.Add(track);
            }
        }

        return tracks;
    }

    private BiblePublicationTrack? ParseSingleTrack(
        JsonElement trackFile,
        string sectionCode,
        string normalizedLanguageCode,
        bool isVideo = false)
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

        // GETPUBMEDIALINKS for drama sections returns a single file per section (pub=sectionCode); no track param.
        var trackUrlParams = new List<UrlParam>
        {
            new UrlParam { Key = "pub", Value = sectionCode, IsQueryParam = true },
            new UrlParam { Key = "fileformat", Value = isVideo ? "mp4" : "mp3", IsQueryParam = true },
            new UrlParam { Key = "alllangs", Value = "0", IsQueryParam = true },
            new UrlParam { Key = "langwritten", Value = normalizedLanguageCode, IsQueryParam = true }
        };

        // For dramas, TrackCode is the sectionCode (pub param value), not the sequential number
        return new BiblePublicationTrack
        {
            TrackCode = sectionCode,
            Title = title,
            UrlParams = trackUrlParams
        };
    }
}
