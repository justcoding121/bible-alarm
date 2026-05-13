#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Cataloger.Utility;

/// <summary>
/// Parses JW.org /en/languages JSON into the set of uppercase sign-language codes.
/// </summary>
internal static class SignLanguageCodesExtractor
{
    public static HashSet<string> ExtractSignLanguageCodes(ILogger logger, JsonDocument cache)
    {
        var signLanguageCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var root = cache.RootElement;

        if (!TryGetLanguagesArrayElement(logger, root, out var languagesArray))
        {
            return signLanguageCodes;
        }

        foreach (var langElement in languagesArray.EnumerateArray())
        {
            if (!langElement.TryGetProperty(AppConstants.Media.LanguageIndexJson.LangCode, out var langcodeElement))
            {
                continue;
            }

            var langcode = langcodeElement.GetString();
            if (string.IsNullOrWhiteSpace(langcode))
            {
                continue;
            }

            var isSignLanguage = false;
            if (langElement.TryGetProperty(AppConstants.Media.LanguageIndexJson.IsSignLanguage, out var isSignLanguageElement))
            {
                isSignLanguage = isSignLanguageElement.GetBoolean();
            }

            if (isSignLanguage)
            {
                signLanguageCodes.Add(langcode.ToUpperInvariant());
            }
        }

        logger.Information("Loaded {Count} sign language codes from /en/languages endpoint", signLanguageCodes.Count);
        return signLanguageCodes;
    }

    private static bool TryGetLanguagesArrayElement(ILogger logger, JsonElement root, out JsonElement languagesArray)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            languagesArray = root;
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty(AppConstants.Media.LanguageIndexJson.Languages, out var languagesProp) && languagesProp.ValueKind == JsonValueKind.Array)
            {
                languagesArray = languagesProp;
                return true;
            }

            if (root.TryGetProperty(AppConstants.Media.LanguageIndexJson.Data, out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
            {
                languagesArray = dataProp;
                return true;
            }

            logger.Warning("Expected JSON array or object with 'languages'/'data' array from /en/languages endpoint");
            languagesArray = default;
            return false;
        }

        logger.Warning("Expected JSON array or object from /en/languages endpoint, got {ValueKind}", root.ValueKind);
        languagesArray = default;
        return false;
    }
}
