#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Harvestors;

internal static class DramaSectionCodeExtractor
{
    internal static (HashSet<string> SectionCodes, string? LocalizedPublicationName) ExtractSectionCodesFromCategory(
        string jsonString,
        string publicationCode,
        string languageCode,
        ILogger logger)
    {
        var sectionCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? localizedPublicationName = null;

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (!root.TryGetProperty("category", out var category))
            {
                return (sectionCodes, null);
            }

            // Extract localized publication name
            // Concatenate parent category name with category name (e.g., "Audio" + "Dramas" = "Audio Dramas")
            string? categoryName = null;
            string? parentCategoryName = null;

            if (category.TryGetProperty("name", out var nameElement))
            {
                var rawName = nameElement.GetString();
                // Decode HTML entities like &nbsp; to proper characters and replace non-breaking spaces with regular spaces
                categoryName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
            }

            if (category.TryGetProperty("parentCategory", out var parentCategoryElement) &&
                parentCategoryElement.TryGetProperty("name", out var parentNameElement))
            {
                var rawParentName = parentNameElement.GetString();
                parentCategoryName = rawParentName != null ? WebUtility.HtmlDecode(rawParentName).Replace('\u00A0', ' ') : null;
            }

            // Concatenate parent category name with category name
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
                return (sectionCodes, localizedPublicationName);
            }

            foreach (var mediaItem in mediaArray.EnumerateArray())
            {
                // Extract section code from naturalKey
                // Pattern: "pub-{sectionCode}_{lang}_{number}_AUDIO"
                // For example: "pub-iaoh_E_12_AUDIO" -> section code is "iaoh"
                string? sectionCode = null;
                if (mediaItem.TryGetProperty("naturalKey", out var naturalKeyElement))
                {
                    var naturalKey = naturalKeyElement.GetString() ?? "";
                    if (naturalKey.StartsWith("pub-", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = naturalKey.Split('_');
                        if (parts.Length > 0)
                        {
                            sectionCode = parts[0].Substring(4); // Remove "pub-" prefix
                        }
                    }
                }

                // If no section code found, skip this item
                if (string.IsNullOrEmpty(sectionCode))
                {
                    logger.Warning("Could not extract section code from naturalKey in publication {PublicationCode} for language {LanguageCode}. Skipping.", publicationCode, languageCode);
                    continue;
                }

                sectionCodes.Add(sectionCode);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to extract section codes from category JSON");
        }

        return (sectionCodes, localizedPublicationName);
    }
}

