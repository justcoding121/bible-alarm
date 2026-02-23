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
        bool isVideo = false,
        int? trackNumber = null,
        bool allowAudioDescriptionTitles = false)
    {
        var tracks = new List<BiblePublicationTrack>();
        var formatKey = isVideo ? "MP4" : "MP3";

        if (!sectionFilesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) ||
            !languageFiles.TryGetProperty(formatKey, out var formatFiles))
        {
            return tracks;
        }

        // For mediator media items (trackNumber set), API may return multiple format entries (e.g. qualities); take only the first to avoid duplicate TrackCodes.
        foreach (var trackFile in formatFiles.EnumerateArray())
        {
            var track = ParseSingleTrack(trackFile, sectionCode, normalizedLanguageCode, isVideo, trackNumber, allowAudioDescriptionTitles);
            if (track != null)
            {
                tracks.Add(track);
                if (trackNumber.HasValue)
                {
                    break;
                }
            }
        }

        return tracks;
    }

    private BiblePublicationTrack? ParseSingleTrack(
        JsonElement trackFile,
        string sectionCode,
        string normalizedLanguageCode,
        bool isVideo = false,
        int? trackNumber = null,
        bool allowAudioDescriptionTitles = false)
    {
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

        if (!allowAudioDescriptionTitles && title.Contains("audio descriptions", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var trackUrlParams = new List<UrlParam>
        {
            new UrlParam { Key = "pub", Value = sectionCode, IsQueryParam = true },
            new UrlParam { Key = "fileformat", Value = isVideo ? "mp4" : "mp3", IsQueryParam = true },
            new UrlParam { Key = "alllangs", Value = "0", IsQueryParam = true },
            new UrlParam { Key = "langwritten", Value = normalizedLanguageCode, IsQueryParam = true }
        };

        if (trackNumber.HasValue)
        {
            trackUrlParams.Add(new UrlParam { Key = "track", Value = trackNumber.Value.ToString(), IsQueryParam = true });
        }

        var trackCode = trackNumber.HasValue ? $"{sectionCode}-{trackNumber.Value}" : sectionCode;
        return new BiblePublicationTrack
        {
            TrackCode = trackCode,
            Title = title,
            UrlParams = trackUrlParams
        };
    }
}
