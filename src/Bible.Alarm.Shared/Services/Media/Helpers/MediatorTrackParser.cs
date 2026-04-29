#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Parsing mediator/video tracks from JSON responses.
/// </summary>
internal static class MediatorTrackParser
{
    public static List<BiblePublicationTrack> ParseTracksFromJson(
        JsonElement sectionFilesElement,
        MediatorTrackParseContext context)
    {
        var tracks = new List<BiblePublicationTrack>();
        var formatKey = context.IsVideo ? AppConstants.Media.MediaStreamFormatMp4 : AppConstants.Media.MediaStreamFormatMp3;

        if (!TryGetLanguageFormatArray(sectionFilesElement, context.NormalizedLanguageCode, formatKey, out var formatFiles))
        {
            return tracks;
        }

        // For mediator media items (trackNumber set), API may return multiple format entries (e.g. qualities); take only the first to avoid duplicate TrackCodes.
        foreach (var trackFile in formatFiles.EnumerateArray())
        {
            var track = ParseSingleTrack(trackFile, context);
            if (track != null)
            {
                tracks.Add(track);
                if (context.TrackNumber.HasValue)
                {
                    break;
                }
            }
        }

        return tracks;
    }

    private static BiblePublicationTrack? ParseSingleTrack(
        JsonElement trackFile,
        MediatorTrackParseContext context)
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

        var title = MediaTrackTitleHelper.UnknownTitle;
        if (trackFile.TryGetProperty("title", out var titleElement))
        {
            if (titleElement.ValueKind == JsonValueKind.String)
            {
                title = MediaTrackTitleHelper.DecodeHtmlTitle(titleElement.GetString());
            }
            else if (titleElement.ValueKind == JsonValueKind.Object && titleElement.TryGetProperty("text", out var titleTextElement))
            {
                title = MediaTrackTitleHelper.DecodeHtmlTitle(titleTextElement.GetString());
            }
        }

        if (!context.AllowAudioDescriptionTitles && AudioDescriptionTitlePhrases.ContainsAudioDescriptionPhrase(context.NormalizedLanguageCode, title))
        {
            return null;
        }

        string trackCode;
        if (context.UseDocidParam && context.SectionCode.StartsWith("docid:", StringComparison.OrdinalIgnoreCase))
        {
            var docidValue = context.SectionCode.Substring(6);
            trackCode = context.TrackNumber.HasValue ? $"{docidValue}-{context.TrackNumber.Value}" : docidValue;
        }
        else
        {
            trackCode = (context.TrackNumber.HasValue && !context.OmitTrackFromUrlParams) ? $"{context.SectionCode}-{context.TrackNumber.Value}" : context.SectionCode;
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

        var formatAlt = formatKey == AppConstants.Media.MediaStreamFormatMp3
            ? AppConstants.Media.MediaStreamFormatMp3Lower
            : AppConstants.Media.MediaStreamFormatMp4Lower;
        return languageFiles.TryGetProperty(formatAlt, out formatFiles);
    }
}
