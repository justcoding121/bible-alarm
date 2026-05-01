#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Cataloger.Utility;

/// <summary>
/// Helper class to check if a language is a sign language by querying the /en/languages endpoint.
/// Caches the response to avoid multiple API calls.
/// </summary>
internal sealed class SignLanguageChecker
{
    private readonly ILogger logger;
    private readonly DownloadUtility downloadUtility;
    private JsonDocument? languagesCache;
    private readonly object languagesCacheLock = new object();
    private Task<JsonDocument>? languagesCacheTask;
    private HashSet<string>? signLanguageCodesCache;

    public SignLanguageChecker(ILogger logger, DownloadUtility downloadUtility)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.downloadUtility = downloadUtility ?? throw new ArgumentNullException(nameof(downloadUtility));
    }

    /// <summary>
    /// Checks if a language code represents a sign language.
    /// </summary>
    /// <param name="languageCode">The language code to check (case-insensitive).</param>
    /// <returns>True if the language is a sign language, false otherwise.</returns>
    public async Task<bool> IsSignLanguageAsync(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return false;
        }

        var normalizedCode = languageCode.ToUpperInvariant();

        // Load cache if needed
        await EnsureCacheLoadedAsync();

        // Check cache
        lock (languagesCacheLock)
        {
            if (signLanguageCodesCache != null)
            {
                return signLanguageCodesCache.Contains(normalizedCode);
            }
        }

        return false;
    }

    /// <summary>
    /// Filters out sign languages from a dictionary of language codes to LanguageInfo.
    /// </summary>
    public async Task<Dictionary<string, LanguageInfo>> FilterSignLanguagesAsync(Dictionary<string, LanguageInfo> languages)
    {
        if (languages == null || languages.Count == 0)
        {
            return languages ?? new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        }

        await EnsureCacheLoadedAsync();

        var filtered = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in languages)
        {
            var isSignLanguage = await IsSignLanguageAsync(kvp.Key);
            if (!isSignLanguage)
            {
                filtered[kvp.Key] = kvp.Value;
            }
            else
            {
                logger.Debug("Filtering out sign language: {LanguageCode} ({LanguageName})", kvp.Key, kvp.Value.Name);
            }
        }

        return filtered;
    }

    /// <summary>
    /// Filters out sign languages from a list of language entries.
    /// </summary>
    public async Task<List<(string Code, string Name, string Direction)>> FilterSignLanguagesAsync(
        List<(string Code, string Name, string Direction)> languageEntries)
    {
        if (languageEntries == null || languageEntries.Count == 0)
        {
            return languageEntries ?? new List<(string Code, string Name, string Direction)>();
        }

        await EnsureCacheLoadedAsync();

        var filtered = new List<(string Code, string Name, string Direction)>();
        foreach (var entry in languageEntries)
        {
            var isSignLanguage = await IsSignLanguageAsync(entry.Code);
            if (!isSignLanguage)
            {
                filtered.Add(entry);
            }
            else
            {
                logger.Debug("Filtering out sign language: {LanguageCode} ({LanguageName})", entry.Code, entry.Name);
            }
        }

        return filtered;
    }

    /// <summary>
    /// Filters out sign languages from a HashSet of language codes.
    /// </summary>
    public async Task<HashSet<string>> FilterSignLanguagesAsync(HashSet<string> languageCodes)
    {
        if (languageCodes == null || languageCodes.Count == 0)
        {
            return languageCodes ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        await EnsureCacheLoadedAsync();

        var filtered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in languageCodes)
        {
            var isSignLanguage = await IsSignLanguageAsync(code);
            if (!isSignLanguage)
            {
                filtered.Add(code);
            }
            else
            {
                logger.Debug("Filtering out sign language: {LanguageCode}", code);
            }
        }

        return filtered;
    }

    private async Task EnsureCacheLoadedAsync()
    {
        Task<JsonDocument>? loadTask = null;
        bool useExistingCache = false;

        lock (languagesCacheLock)
        {
            if (languagesCache != null && signLanguageCodesCache != null)
            {
                useExistingCache = true;
            }
            else if (languagesCacheTask != null)
            {
                loadTask = languagesCacheTask;
            }
            else
            {
                loadTask = LoadLanguagesCacheAsync();
                languagesCacheTask = loadTask;
            }
        }

        JsonDocument cache;
        if (useExistingCache)
        {
            return;
        }
        else
        {
            cache = await loadTask!;

            lock (languagesCacheLock)
            {
                if (languagesCache == null)
                {
                    languagesCache = cache;
                    signLanguageCodesCache = BuildSignLanguageCodesCache(cache);
                }
            }
        }
    }

    private async Task<JsonDocument> LoadLanguagesCacheAsync()
    {
        logger.Debug("Fetching languages from JW.org /en/languages endpoint to check for sign languages...");
        var url = AppConstants.ApiEndpoints.JwOrgLanguagesListUrl;
        var jsonString = await downloadUtility.GetAsync(url);
        return JsonDocument.Parse(jsonString);
    }

    private HashSet<string> BuildSignLanguageCodesCache(JsonDocument cache)
    {
        var signLanguageCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var root = cache.RootElement;

        if (!TryGetLanguagesArrayElement(root, out var languagesArray))
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
                var normalizedCode = langcode.ToUpperInvariant();
                signLanguageCodes.Add(normalizedCode);
            }
        }

        logger.Information("Loaded {Count} sign language codes from /en/languages endpoint", signLanguageCodes.Count);
        return signLanguageCodes;
    }

    private bool TryGetLanguagesArrayElement(JsonElement root, out JsonElement languagesArray)
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
