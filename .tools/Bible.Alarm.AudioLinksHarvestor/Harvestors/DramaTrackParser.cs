#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using Bible.Alarm.AudioLinksHarvestor.Models;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Harvestors;

internal static class DramaTrackParser
{
    internal static (List<DramaTrack>? Tracks, string? SectionName) ParseTracksFromGetPubMediaLinks(
        string jsonString,
        string sectionCode,
        string languageCode,
        ILogger logger)
    {
        var tracks = new List<DramaTrack>();
        string? sectionName = null;

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
            {
                return (null, null);
            }

            // Extract section name from pubName field
            if (root.TryGetProperty("pubName", out var pubNameElement))
            {
                var rawName = pubNameElement.GetString();
                // Decode HTML entities like &nbsp; to proper characters and replace non-breaking spaces with regular spaces
                sectionName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
            }

            if (!filesElement.TryGetProperty(languageCode, out var languageFiles) ||
                !languageFiles.TryGetProperty("MP3", out var mp3Files))
            {
                return (null, sectionName);
            }

            var trackCode = 1;
            foreach (var trackFile in mp3Files.EnumerateArray())
            {
                if (!trackFile.TryGetProperty("file", out var fileElement))
                {
                    continue;
                }

                // Handle both cases: file can be a string (direct URL) or an object with a "url" property
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

                // Get track title
                string title = "Unknown";
                if (trackFile.TryGetProperty("title", out var titleElement))
                {
                    // Handle both cases: title can be a string or an object
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

                // Build lookup path using GETPUBMEDIALINKS format (no track param for drama sections)
                var lookUpPath = $"?output=json&pub={sectionCode}&fileformat=MP3&alllangs=0&langwritten={languageCode}";

                tracks.Add(new DramaTrack
                {
                    Number = trackCode,
                    Title = title,
                    Url = url,
                    LookUpPath = lookUpPath
                });

                trackCode++;
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

