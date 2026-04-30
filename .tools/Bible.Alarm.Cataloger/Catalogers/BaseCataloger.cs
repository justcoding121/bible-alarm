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

/// <summary>
/// Base class for all catalogers providing common functionality and constants.
/// </summary>
internal abstract class BaseCataloger
{
    /// <summary>
    /// Maximum number of concurrent language downloads across all catalogers.
    /// </summary>
    protected const int MaxConcurrentLanguageDownloads = 8;

    /// <summary>
    /// Language codes to process during test runs (English, Malayalam, Arabic).
    /// </summary>
    protected static readonly HashSet<string> TestRunLanguageCodes = [AppConstants.Media.DefaultLanguageCode, "MY", "A"]; // A = Arabic, not AR (Bambara)

    /// <summary>
    /// Logger instance for logging operations.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Download utility for making HTTP requests with retry logic.
    /// </summary>
    protected readonly DownloadUtility DownloadUtility;

    /// <summary>
    /// Initializes a new instance of the BaseCataloger class.
    /// </summary>
    /// <param name="logger">Logger instance for logging operations.</param>
    /// <param name="downloadUtility">Download utility for making HTTP requests.</param>
    protected BaseCataloger(ILogger logger, DownloadUtility downloadUtility)
    {
        Logger = logger;
        DownloadUtility = downloadUtility;
    }

    /// <summary>
    /// Parses language entries from a JSON response.
    /// </summary>
    /// <param name="jsonString">The JSON string to parse.</param>
    /// <returns>List of language entries with Code, Name, and Direction, or null if parsing fails.</returns>
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
            foreach (var element in languages.EnumerateArray())
            {
                string? languageCode = null;
                if (element.TryGetProperty(AppConstants.Media.LanguageIndexJson.LangCode, out var lc))
                {
                    languageCode = lc.GetString()?.ToUpperInvariant();
                }
                else if (element.TryGetProperty(AppConstants.Media.LanguageIndexJson.Symbol, out var sym))
                {
                    languageCode = sym.GetString()?.ToUpperInvariant();
                }

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

                var direction = AppConstants.Media.TextDirectionLeftToRight;
                if (element.TryGetProperty(AppConstants.Media.LanguageIndexJson.Direction, out var directionElement))
                {
                    direction = directionElement.GetString() ?? AppConstants.Media.TextDirectionLeftToRight;
                }

                languageEntries.Add((languageCode, language, direction));
            }
        }
        else if (languages.ValueKind == JsonValueKind.Object)
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

                var direction = AppConstants.Media.TextDirectionLeftToRight;
                if (item.Value.TryGetProperty(AppConstants.Media.LanguageIndexJson.Direction, out var directionElement))
                {
                    direction = directionElement.GetString() ?? AppConstants.Media.TextDirectionLeftToRight;
                }

                languageEntries.Add((languageCode, language, direction));
            }
        }

        var hasEntries = languageEntries.Count > 0;
        return hasEntries ? languageEntries : null;
    }

    /// <summary>
    /// Filters language entries for test run mode.
    /// </summary>
    /// <param name="languageEntries">The language entries to filter.</param>
    /// <param name="isTestRun">Whether test run mode is enabled.</param>
    /// <returns>Filtered list of language entries, or null if empty.</returns>
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
    /// <param name="languageName">The name of the language to check.</param>
    /// <returns>True if the language should be skipped, false otherwise.</returns>
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
