#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Cataloger.Models;
using Serilog;

namespace Bible.Alarm.Cataloger.Catalogers;

internal static class MediatorCategoryLanguageExtractor
{
    internal static HashSet<string> ExtractLanguagesFromCategory(string jsonString, ILogger logger)
    {
        // Use case-insensitive HashSet to avoid duplicates from case differences
        var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (!root.TryGetProperty(AppConstants.Media.PubMediaJson.Category, out var category))
            {
                return languages;
            }

            if (!category.TryGetProperty(AppConstants.Media.PubMediaJson.Media, out var mediaArray))
            {
                return languages;
            }

            foreach (var mediaItem in mediaArray.EnumerateArray())
            {
                if (!mediaItem.TryGetProperty(AppConstants.Media.PubMediaJson.AvailableLanguages, out var availableLanguages))
                {
                    continue;
                }

                foreach (var lang in availableLanguages.EnumerateArray())
                {
                    var langCode = lang.GetString();
                    if (!string.IsNullOrEmpty(langCode))
                    {
                        // Normalize to uppercase for consistent storage
                        languages.Add(langCode.ToUpperInvariant());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to parse languages from category JSON");
        }

        return languages;
    }

    internal static Dictionary<string, LanguageInfo> ExtractLanguageInfoFromCategory(
        string jsonString,
        HashSet<string> languageCodes,
        ILogger logger)
    {
        var languageInfoMap = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (!root.TryGetProperty(AppConstants.Media.PubMediaJson.Category, out var category))
            {
                return languageInfoMap;
            }

            // Try to get language info from category.language if available
            if (category.TryGetProperty(AppConstants.Media.PubMediaJson.Language, out var languageElement))
            {
                var direction = AppConstants.Media.TextDirectionLeftToRight;
                if (languageElement.TryGetProperty(AppConstants.Media.LanguageIndexJson.Direction, out var dirElement))
                {
                    direction = dirElement.GetString() ?? AppConstants.Media.TextDirectionLeftToRight;
                }

                string? name = null;
                if (languageElement.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var nameElement))
                {
                    var rawName = nameElement.GetString();
                    name = MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
                }

                // This is for English, add it
                if (!string.IsNullOrEmpty(name))
                {
                    languageInfoMap[AppConstants.Media.DefaultLanguageCode] = new LanguageInfo(name, direction);
                }
            }

            // For other languages, we'll use defaults (name = code, direction = ltr)
            // They can be updated when fetched on-demand
            foreach (var langCode in languageCodes.Where(c => !languageInfoMap.ContainsKey(c)))
            {
                languageInfoMap[langCode] = new LanguageInfo(langCode, AppConstants.Media.TextDirectionLeftToRight);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to extract language info from category JSON");
        }

        return languageInfoMap;
    }
}

