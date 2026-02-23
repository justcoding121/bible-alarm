#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Harvestors;

internal static class DramaSectionCodeExtractor
{
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

