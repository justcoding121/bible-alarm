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
/// Helper class for parsing mediator/video tracks from JSON responses.
/// </summary>
internal sealed class MediatorTrackParser
{
    private readonly ILogger logger;

    public MediatorTrackParser(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public List<BiblePublicationTrack> ParseTracksFromJson(
        JsonElement sectionFilesElement,
        string normalizedLanguageCode,
        string sectionCode,
        bool isVideo = false,
        int? trackNumber = null,
        bool allowAudioDescriptionTitles = false,
        bool useIssueParameter = false,
        bool omitTrackFromUrlParams = false,
        bool useDocidParam = false)
    {
        var tracks = new List<BiblePublicationTrack>();
        var formatKey = isVideo ? "MP4" : "MP3";

        if (!TryGetLanguageFormatArray(sectionFilesElement, normalizedLanguageCode, formatKey, out var formatFiles))
        {
            return tracks;
        }

        // For mediator media items (trackNumber set), API may return multiple format entries (e.g. qualities); take only the first to avoid duplicate TrackCodes.
        foreach (var trackFile in formatFiles.EnumerateArray())
        {
            var track = ParseSingleTrack(trackFile, sectionCode, normalizedLanguageCode, isVideo, trackNumber, allowAudioDescriptionTitles, useIssueParameter, omitTrackFromUrlParams, useDocidParam);
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
        bool allowAudioDescriptionTitles = false,
        bool useIssueParameter = false,
        bool omitTrackFromUrlParams = false,
        bool useDocidParam = false)
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

        if (!allowAudioDescriptionTitles && AudioDescriptionTitlePhrases.ContainsAudioDescriptionPhrase(normalizedLanguageCode, title))
        {
            return null;
        }

        string trackCode;
        if (useDocidParam && sectionCode.StartsWith("docid:", StringComparison.OrdinalIgnoreCase))
        {
            var docidValue = sectionCode.Substring(6);
            trackCode = trackNumber.HasValue ? $"{docidValue}-{trackNumber.Value}" : docidValue;
        }
        else
        {
            trackCode = (trackNumber.HasValue && !omitTrackFromUrlParams) ? $"{sectionCode}-{trackNumber.Value}" : sectionCode;
        }

        return new BiblePublicationTrack
        {
            TrackCode = trackCode,
            Title = title,
            TrackUrl = new TrackUrl { Url = url }
        };
    }

    private static bool TryGetLanguageFormatArray(JsonElement sectionFilesElement, string languageCode, string formatKey, out JsonElement formatFiles)
    {
        formatFiles = default;
        var gotLang = sectionFilesElement.TryGetProperty(languageCode, out var languageFiles);
        if (!gotLang)
        {
            gotLang = sectionFilesElement.TryGetProperty(languageCode.ToLowerInvariant(), out languageFiles);
        }

        if (!gotLang)
        {
            gotLang = sectionFilesElement.TryGetProperty(languageCode.ToUpperInvariant(), out languageFiles);
        }

        if (!gotLang)
        {
            return false;
        }

        if (languageFiles.TryGetProperty(formatKey, out formatFiles))
        {
            return true;
        }

        var formatAlt = formatKey == "MP3" ? "mp3" : "mp4";
        return languageFiles.TryGetProperty(formatAlt, out formatFiles);
    }
}
