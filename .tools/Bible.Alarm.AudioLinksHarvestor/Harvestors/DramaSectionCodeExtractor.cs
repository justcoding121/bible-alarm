#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using Bible.Alarm.AudioLinksHarvestor.Models;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Harvestors;

internal static class DramaSectionCodeExtractor
{
    /// <summary>
    /// Extracts tracks with CDN URLs directly from the mediator response. Use this for mediator publications;
    /// do not call GETPUBMEDIALINKS when using mediator.
    /// </summary>
    internal static (List<DramaTrack> Tracks, string? LocalizedPublicationName) ExtractTracksFromMediatorCategory(
        string jsonString,
        string publicationCode,
        string languageCode,
        ILogger logger)
    {
        var tracks = new List<DramaTrack>();
        string? localizedPublicationName = null;

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (!root.TryGetProperty("category", out var category))
            {
                return (tracks, null);
            }

            if (category.TryGetProperty("name", out var nameElement))
            {
                var rawName = nameElement.GetString();
                var categoryName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                if (category.TryGetProperty("parentCategory", out var parentCategoryElement) &&
                    parentCategoryElement.TryGetProperty("name", out var parentNameElement))
                {
                    var rawParent = parentNameElement.GetString();
                    var parentName = rawParent != null ? WebUtility.HtmlDecode(rawParent).Replace('\u00A0', ' ') : null;
                    if (!string.IsNullOrEmpty(parentName) && !string.IsNullOrEmpty(categoryName))
                        localizedPublicationName = $"{parentName} {categoryName}";
                    else if (!string.IsNullOrEmpty(categoryName))
                        localizedPublicationName = categoryName;
                }
                else if (!string.IsNullOrEmpty(categoryName))
                {
                    localizedPublicationName = categoryName;
                }
            }

            if (!category.TryGetProperty("media", out var mediaArray))
            {
                return (tracks, localizedPublicationName);
            }

            var categoryKey = publicationCode;
            var lookUpPathBase = $"?category={categoryKey}&lang={languageCode}";

            foreach (var mediaItem in mediaArray.EnumerateArray())
            {
                if (!mediaItem.TryGetProperty("naturalKey", out var naturalKeyElement))
                    continue;

                var naturalKey = naturalKeyElement.GetString() ?? "";
                string trackCode;
                if (naturalKey.StartsWith("docid-", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = naturalKey.Split('_');
                    if (parts.Length < 3 || !int.TryParse(parts[2], out var docidTrack) || docidTrack < 1)
                        continue;
                    var docidValue = parts[0].Substring(6);
                    if (string.IsNullOrEmpty(docidValue)) continue;
                    trackCode = $"{docidValue}-{docidTrack}";
                }
                else if (naturalKey.StartsWith("pub-", StringComparison.OrdinalIgnoreCase))
                {
                    var pubParts = naturalKey.Split('_');
                    if (pubParts.Length < 3) continue;
                    var sectionCode = pubParts[0].Substring(4);
                    var trackPart = pubParts[2];
                    var trackNumber = int.TryParse(trackPart, out var n) && n >= 1 ? n : (string.Equals(trackPart, "x", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
                    if (trackNumber < 1) continue;
                    trackCode = $"{sectionCode}-{trackNumber}";
                }
                else
                    continue;

                if (!mediaItem.TryGetProperty("files", out var filesElement) || filesElement.ValueKind != JsonValueKind.Array)
                    continue;

                string? url = null;
                foreach (var file in filesElement.EnumerateArray())
                {
                    if (file.TryGetProperty("progressiveDownloadURL", out var urlEl))
                    {
                        url = urlEl.GetString();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(url))
                    continue;

                var title = "Unknown";
                if (mediaItem.TryGetProperty("title", out var titleElement))
                {
                    var rawTitle = titleElement.GetString();
                    title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
                }

                tracks.Add(new DramaTrack
                {
                    TrackCode = trackCode,
                    Title = title,
                    Url = url,
                    LookUpPath = lookUpPathBase
                });
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to extract tracks from mediator category JSON");
        }

        return (tracks, localizedPublicationName);
    }

    internal static (List<(string SectionCode, int TrackNumber)> MediaItems, string? LocalizedPublicationName) ExtractMediaItemsFromCategory(
        string jsonString,
        string publicationCode,
        string languageCode,
        ILogger logger)
    {
        var mediaItems = new List<(string SectionCode, int TrackNumber)>();
        string? localizedPublicationName = null;

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (!root.TryGetProperty("category", out var category))
            {
                return (mediaItems, null);
            }

            string? categoryName = null;
            string? parentCategoryName = null;

            if (category.TryGetProperty("name", out var nameElement))
            {
                var rawName = nameElement.GetString();
                categoryName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
            }

            if (category.TryGetProperty("parentCategory", out var parentCategoryElement) &&
                parentCategoryElement.TryGetProperty("name", out var parentNameElement))
            {
                var rawParentName = parentNameElement.GetString();
                parentCategoryName = rawParentName != null ? WebUtility.HtmlDecode(rawParentName).Replace('\u00A0', ' ') : null;
            }

            if (!string.IsNullOrEmpty(parentCategoryName) && !string.IsNullOrEmpty(categoryName))
            {
                localizedPublicationName = $"{parentCategoryName} {categoryName}";
            }
            else if (!string.IsNullOrEmpty(categoryName))
            {
                localizedPublicationName = categoryName;
            }

            if (!category.TryGetProperty("media", out var mediaArray))
            {
                return (mediaItems, localizedPublicationName);
            }

            foreach (var mediaItem in mediaArray.EnumerateArray())
            {
                if (!mediaItem.TryGetProperty("naturalKey", out var naturalKeyElement))
                {
                    continue;
                }

                var naturalKey = naturalKeyElement.GetString() ?? "";
                if (naturalKey.StartsWith("docid-", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = naturalKey.Split('_');
                    if (parts.Length < 3)
                    {
                        continue;
                    }

                    var docidValue = parts[0].Substring(6);
                    if (string.IsNullOrEmpty(docidValue) || !int.TryParse(docidValue, out _))
                    {
                        continue;
                    }

                    if (!int.TryParse(parts[2], out var docidTrack) || docidTrack < 1)
                    {
                        continue;
                    }

                    mediaItems.Add(($"docid:{docidValue}", docidTrack));
                    continue;
                }

                if (!naturalKey.StartsWith("pub-", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var pubParts = naturalKey.Split('_');
                if (pubParts.Length < 3)
                {
                    continue;
                }

                var sectionCode = pubParts[0].Substring(4);
                if (string.IsNullOrEmpty(sectionCode))
                {
                    continue;
                }

                var trackPart = pubParts[2];
                if (!int.TryParse(trackPart, out var trackNumber) || trackNumber < 1)
                {
                    if (string.Equals(trackPart, "x", StringComparison.OrdinalIgnoreCase))
                    {
                        trackNumber = 1;
                    }
                    else
                    {
                        logger.Warning("Could not parse track number from naturalKey in publication {PublicationCode} for language {LanguageCode}. Skipping.", publicationCode, languageCode);
                        continue;
                    }
                }

                mediaItems.Add((sectionCode, trackNumber));
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to extract media items from category JSON");
        }

        return (mediaItems, localizedPublicationName);
    }
}

