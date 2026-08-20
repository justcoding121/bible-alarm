#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Serilog;

namespace Bible.Alarm.Cataloger.Catalogers;

internal abstract class BaseCataloger
{
    protected const int MaxConcurrentLanguageDownloads = 8;

    protected static readonly HashSet<string> TestRunLanguageCodes = new(
        [AppConstants.Media.DefaultLanguageCode, "MY", "A"], // A = Arabic, not AR (Bambara)
        StringComparer.OrdinalIgnoreCase);

    protected readonly ILogger Logger;

    protected readonly DownloadUtility DownloadUtility;

    protected BaseCataloger(ILogger logger, DownloadUtility downloadUtility)
    {
        Logger = logger;
        DownloadUtility = downloadUtility;
    }

    [SuppressMessage("SonarAnalyzer.CSharp", "S2583", Justification = "languageEntries can be populated from Array or Object JSON; both return branches are reachable.")]
    protected static List<(string Code, string Name, string Direction)>? ParseLanguageEntries(string jsonString)
    {
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(AppConstants.Media.LanguageIndexJson.Languages, out var languages))
        {
            return null;
        }

        if (languages.ValueKind != JsonValueKind.Array && languages.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var languageEntries = new List<(string Code, string Name, string Direction)>();

        if (languages.ValueKind == JsonValueKind.Array)
        {
            AppendLanguageEntriesFromLanguagesArray(languages, languageEntries);
        }
        else if (languages.ValueKind == JsonValueKind.Object)
        {
            AppendLanguageEntriesFromLanguagesObject(languages, languageEntries);
        }

        var hasEntries = languageEntries.Count > 0;
        return hasEntries ? languageEntries : null;
    }

    private static void AppendLanguageEntriesFromLanguagesArray(
        JsonElement languages,
        List<(string Code, string Name, string Direction)> languageEntries)
    {
        foreach (var element in languages.EnumerateArray())
        {
            var languageCode = ResolveLanguageCodeFromArrayElement(element);
            if (string.IsNullOrEmpty(languageCode))
            {
                continue;
            }

            if (!element.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var nameElement))
            {
                continue;
            }

            var rawLanguage = nameElement.GetString();
            var language = MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawLanguage);
            if (string.IsNullOrEmpty(language))
            {
                continue;
            }

            var direction = ReadLanguageDirection(element);
            languageEntries.Add((languageCode, language, direction));
        }
    }

    private static void AppendLanguageEntriesFromLanguagesObject(
        JsonElement languages,
        List<(string Code, string Name, string Direction)> languageEntries)
    {
        foreach (var item in languages.EnumerateObject())
        {
            var languageCode = item.Name.ToUpperInvariant();

            if (!item.Value.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var nameElement))
            {
                continue;
            }

            var rawLanguage = nameElement.GetString();
            var language = MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawLanguage);
            if (string.IsNullOrEmpty(language))
            {
                continue;
            }

            var direction = ReadLanguageDirection(item.Value);
            languageEntries.Add((languageCode, language, direction));
        }
    }

    private static string? ResolveLanguageCodeFromArrayElement(JsonElement element)
    {
        if (element.TryGetProperty(AppConstants.Media.LanguageIndexJson.LangCode, out var lc))
        {
            return lc.GetString()?.ToUpperInvariant();
        }

        if (element.TryGetProperty(AppConstants.Media.LanguageIndexJson.Symbol, out var sym))
        {
            return sym.GetString()?.ToUpperInvariant();
        }

        return null;
    }

    private static string ReadLanguageDirection(JsonElement element)
    {
        var direction = AppConstants.Media.TextDirectionLeftToRight;
        if (element.TryGetProperty(AppConstants.Media.LanguageIndexJson.Direction, out var directionElement))
        {
            direction = directionElement.GetString() ?? AppConstants.Media.TextDirectionLeftToRight;
        }

        return direction;
    }

    protected static List<(string Code, string Name, string Direction)>? FilterLanguageEntriesForTestRun(
        List<(string Code, string Name, string Direction)>? languageEntries,
        bool isTestRun)
    {
        if (languageEntries == null || languageEntries.Count == 0)
        {
            return null;
        }

        if (!isTestRun)
        {
            return languageEntries;
        }

        var testRunEntries = languageEntries.Where(e => TestRunLanguageCodes.Contains(e.Code)).ToList();
        return testRunEntries.Count > 0 ? testRunEntries : null;
    }

    /// <summary>
    /// Checks if a language should be skipped (e.g., sign languages).
    /// </summary>
    protected static bool ShouldSkipLanguage(string languageName)
    {
        if (string.IsNullOrWhiteSpace(languageName))
        {
            return false;
        }

        var lowerName = languageName.ToLowerInvariant();
        return lowerName.Contains("sign language", StringComparison.Ordinal);
    }
}
