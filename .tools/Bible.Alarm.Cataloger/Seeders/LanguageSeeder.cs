#nullable enable
using System;
using System.Linq;
using System.Text.Json;
using Bible.Alarm.Shared.Helpers;
using System.Threading.Tasks;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Language = Bible.Alarm.Shared.Models.Media.Language;

namespace Bible.Alarm.Cataloger.Seeders;

/// <summary>
/// Helper class for seeding languages from JW.org API.
/// </summary>
internal sealed class LanguageSeeder
{
    private readonly ILogger logger;
    private readonly DownloadUtility downloadUtility;
    private JsonDocument? languagesCache;
    private readonly object languagesCacheLock = new object();
    private Task<JsonDocument>? languagesCacheTask;

    public LanguageSeeder(ILogger logger, DownloadUtility downloadUtility)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.downloadUtility = downloadUtility ?? throw new ArgumentNullException(nameof(downloadUtility));
    }

    /// <summary>
    /// Gets an existing language by code (case-insensitive) or creates one by looking it up from /en/languages API.
    /// </summary>
    public async Task<Language> GetOrCreateLanguageByCode(MediaDbContext db, string code)
    {
        var normalizedCode = code.ToUpperInvariant();

        var language = await db.Languages.FirstOrDefaultAsync(x =>
            string.Equals(x.LanguageCode, normalizedCode, StringComparison.OrdinalIgnoreCase));
        if (language == null)
        {
            var (name, direction) = await FetchLanguageInfoFromJwOrgLanguagesApi(normalizedCode);
            
            language = new Language
            {
                LanguageCode = normalizedCode,
                Direction = direction
            };
            db.Languages.Add(language);
            await db.SaveChangesAsync();

            var displayName = name ?? normalizedCode;
            db.LanguageNamesByLanguage.Add(new LanguageNameByLanguage
            {
                LanguageId = language.Id,
                DisplayLanguageCode = AppConstants.Media.DefaultLanguageCode,
                Name = displayName
            });
            await db.SaveChangesAsync();

            logger.Information("Created new language {Code}: Name={Name}, Direction={Direction} from /en/languages API",
                normalizedCode, displayName, direction);
        }
        return language;
    }

    private async Task<JsonDocument> GetOrAwaitLanguagesCacheDocumentAsync()
    {
        Task<JsonDocument>? loadTask = null;
        var useExistingCache = false;

        lock (languagesCacheLock)
        {
            if (languagesCache != null)
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
            lock (languagesCacheLock)
            {
                cache = languagesCache!;
            }
        }
        else
        {
            cache = await loadTask!;

            lock (languagesCacheLock)
            {
                languagesCache ??= cache;
            }
        }

        return cache;
    }

    private bool TryResolveLanguagesArrayFromRoot(
        JsonElement root,
        out JsonElement languagesArray,
        bool logUnexpectedObjectPropertyNames)
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

            if (logUnexpectedObjectPropertyNames)
            {
                var properties = root.EnumerateObject().Select(p => p.Name).ToList();
                logger.Warning("Expected JSON array or object with 'languages'/'data' array from /en/languages endpoint. Got object with properties: {Properties}", string.Join(", ", properties));
            }
            else
            {
                logger.Warning("Expected JSON array or object with 'languages'/'data' array from /en/languages endpoint");
            }

            languagesArray = default;
            return false;
        }

        logger.Warning("Expected JSON array or object from /en/languages endpoint, got {ValueKind}", root.ValueKind);
        languagesArray = default;
        return false;
    }

    private static (string Name, string Direction) ReadDisplayNameAndDirectionFromLangElement(JsonElement langElement, string defaultName)
    {
        var name = defaultName;
        if (langElement.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var nameElement))
        {
            var rawName = nameElement.GetString();
            if (!string.IsNullOrWhiteSpace(rawName))
            {
                name = MediaTrackTitleHelper.DecodeHtmlTitle(rawName);
                if (name.Length > 100)
                {
                    name = name[..100];
                }
            }
        }

        var direction = AppConstants.Media.TextDirectionLeftToRight;
        if (langElement.TryGetProperty(AppConstants.Media.LanguageIndexJson.Direction, out var directionElement))
        {
            var dirValue = directionElement.GetString();
            if (!string.IsNullOrWhiteSpace(dirValue))
            {
                direction = dirValue;
            }
        }

        return (name, direction);
    }

    private (string? Name, string Direction) FindLanguageEntryInArray(JsonElement languagesArray, string languageCode)
    {
        foreach (var langElement in languagesArray.EnumerateArray())
        {
            if (!langElement.TryGetProperty(AppConstants.Media.LanguageIndexJson.LangCode, out var langcodeElement))
            {
                continue;
            }

            var langcode = langcodeElement.GetString();
            if (string.IsNullOrWhiteSpace(langcode) ||
                !langcode.Equals(languageCode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var (name, direction) = ReadDisplayNameAndDirectionFromLangElement(langElement, languageCode);
            return (name, direction);
        }

        logger.Debug("Language {LanguageCode} not found in /en/languages API response", languageCode);
        return (null, AppConstants.Media.TextDirectionLeftToRight);
    }

    /// <summary>
    /// Fetches language name and direction from JW.org /en/languages endpoint.
    /// Caches the response to avoid multiple API calls. Thread-safe and async-safe implementation.
    /// </summary>
    public async Task<(string? Name, string Direction)> FetchLanguageInfoFromJwOrgLanguagesApi(string languageCode)
    {
        try
        {
            var cache = await GetOrAwaitLanguagesCacheDocumentAsync();
            if (!TryResolveLanguagesArrayFromRoot(cache.RootElement, out var languagesArray, logUnexpectedObjectPropertyNames: true))
            {
                return (null, AppConstants.Media.TextDirectionLeftToRight);
            }

            return FindLanguageEntryInArray(languagesArray, languageCode);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to fetch language info for {LanguageCode} from /en/languages API, using default ltr", languageCode);
            return (null, AppConstants.Media.TextDirectionLeftToRight);
        }
    }

    private async Task<JsonDocument> LoadLanguagesCacheAsync()
    {
        logger.Debug("Fetching languages from JW.org /en/languages endpoint for lookup...");
        var url = AppConstants.ApiEndpoints.JwOrgLanguagesListUrl;
        var jsonString = await downloadUtility.GetAsync(url);
        return JsonDocument.Parse(jsonString);
    }

    private readonly record struct SeedAllLangDeltas(int SeededDelta, int SkippedDelta, int SignSkippedDelta);

    private async Task<SeedAllLangDeltas> TryProcessSeedAllLanguagesElementAsync(MediaDbContext db, JsonElement langElement)
    {
        if (!langElement.TryGetProperty(AppConstants.Media.LanguageIndexJson.LangCode, out var langcodeElement))
        {
            return default;
        }

        var langcode = langcodeElement.GetString();
        if (string.IsNullOrWhiteSpace(langcode))
        {
            return default;
        }

        var isSignLanguage = false;
        if (langElement.TryGetProperty(AppConstants.Media.LanguageIndexJson.IsSignLanguage, out var isSignLanguageElement))
        {
            isSignLanguage = isSignLanguageElement.GetBoolean();
        }

        if (isSignLanguage)
        {
            logger.Debug("Skipping sign language: {LanguageCode}", langcode);
            return new SeedAllLangDeltas(0, 0, 1);
        }

        var normalizedCode = langcode.ToUpperInvariant();

        var existingLanguage = await db.Languages
            .FirstOrDefaultAsync(l => l.LanguageCode == normalizedCode);

        if (existingLanguage != null)
        {
            return new SeedAllLangDeltas(0, 1, 0);
        }

        var (name, direction) = ReadDisplayNameAndDirectionFromLangElement(langElement, normalizedCode);

        var language = new Language
        {
            LanguageCode = normalizedCode,
            Direction = direction
        };
        db.Languages.Add(language);
        await db.SaveChangesAsync();

        db.LanguageNamesByLanguage.Add(new LanguageNameByLanguage
        {
            LanguageId = language.Id,
            DisplayLanguageCode = AppConstants.Media.DefaultLanguageCode,
            Name = name
        });

        return new SeedAllLangDeltas(1, 0, 0);
    }

    /// <summary>
    /// Seeds ALL languages from jw.org /en/languages API to the Languages table.
    /// This ensures all languages are available in the database, not just discovered ones.
    /// Excludes sign languages.
    /// </summary>
    public async Task SeedAllLanguagesFromJwOrg(MediaDbContext db)
    {
        logger.Information("=== Seeding all languages from jw.org /en/languages API (excluding sign languages) ===");

        try
        {
            var cache = await LoadLanguagesCacheAsync();
            if (!TryResolveLanguagesArrayFromRoot(cache.RootElement, out var languagesArray, logUnexpectedObjectPropertyNames: false))
            {
                return;
            }

            var languagesSeeded = 0;
            var languagesSkipped = 0;
            var signLanguagesSkipped = 0;

            foreach (var langElement in languagesArray.EnumerateArray())
            {
                var d = await TryProcessSeedAllLanguagesElementAsync(db, langElement);
                languagesSeeded += d.SeededDelta;
                languagesSkipped += d.SkippedDelta;
                signLanguagesSkipped += d.SignSkippedDelta;
            }

            await db.SaveChangesAsync();

            logger.Information("✓ Seeded {SeededCount} languages from jw.org API ({SkippedCount} already existed, {SignLanguagesSkipped} sign languages excluded)",
                languagesSeeded, languagesSkipped, signLanguagesSkipped);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to seed all languages from jw.org API", ex);
        }
    }
}
