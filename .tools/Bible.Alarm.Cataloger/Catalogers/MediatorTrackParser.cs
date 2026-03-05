#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Shared.Helpers;
using Serilog;

namespace Bible.Alarm.Cataloger.Catalogers;

internal static class MediatorTrackParser
{
    internal static (List<MediatorTrack>? Tracks, string? SectionName) ParseTracksFromGetPubMediaLinks(
        string jsonString,
        string sectionCode,
        string languageCode,
        ILogger logger,
        bool isVideo = false,
        int? trackNumber = null,
        bool useIssueParameter = false,
        bool useDocidParam = false)
    {
        var tracks = new List<MediatorTrack>();
        string? sectionName = null;
        var formatKey = isVideo ? "MP4" : "MP3";

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
            {
                return (null, null);
            }

            if (root.TryGetProperty("pubName", out var pubNameElement))
            {
                var rawName = pubNameElement.GetString();
                sectionName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
            }

            if (!filesElement.TryGetProperty(languageCode, out var languageFiles) ||
                !languageFiles.TryGetProperty(formatKey, out var formatFiles))
            {
                return (null, sectionName);
            }

            var fileFormat = isVideo ? "MP4" : "MP3";
            string lookUpPathBase;
            if (useDocidParam && sectionCode.StartsWith("docid:", StringComparison.OrdinalIgnoreCase))
            {
                var docidValue = sectionCode.Substring(6);
                lookUpPathBase = $"?output=json&docid={docidValue}&fileformat={fileFormat}&alllangs=0&langwritten={languageCode}";
            }
            else
            {
                lookUpPathBase = $"?output=json&pub={sectionCode}&fileformat={fileFormat}&alllangs=0&langwritten={languageCode}";
            }

            var numberParam = useIssueParameter ? "issue" : "track";

            foreach (var trackFile in formatFiles.EnumerateArray())
            {
                if (!trackFile.TryGetProperty("file", out var fileElement))
                {
                    continue;
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
                    continue;
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

                if (AudioDescriptionTitlePhrases.ContainsAudioDescriptionPhrase(languageCode, title))
                {
                    continue;
                }

                string trackCode;
                if (useDocidParam && sectionCode.StartsWith("docid:", StringComparison.OrdinalIgnoreCase))
                    trackCode = trackNumber.HasValue ? $"{sectionCode.Substring(6)}-{trackNumber.Value}" : sectionCode.Substring(6);
                else
                    trackCode = trackNumber.HasValue ? $"{sectionCode}-{trackNumber.Value}" : sectionCode;
                var lookUpPath = lookUpPathBase;

                tracks.Add(new MediatorTrack
                {
                    TrackCode = trackCode,
                    Title = title,
                    Url = url,
                    LookUpPath = lookUpPath
                });
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to parse tracks from GETPUBMEDIALINKS JSON for section {SectionCode}", sectionCode);
            return (null, null);
        }

        return tracks.Count > 0 ? (tracks, sectionName) : (null, sectionName);
    }
}

