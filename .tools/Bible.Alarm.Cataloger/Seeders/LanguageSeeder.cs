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
    private JsonDocument? languagesCache; // Cache for /en/languages API response
    private readonly object languagesCacheLock = new object(); // Lock for thread-safe cache access
    private Task<JsonDocument>? languagesCacheTask; // Task for async-safe cache loading

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
        // Normalize code to uppercase for consistent storage and comparison
        var normalizedCode = code.ToUpperInvariant();

        // Case-insensitive lookup by code only
        var language = await db.Languages.FirstOrDefaultAsync(x =>
            string.Equals(x.LanguageCode, normalizedCode, StringComparison.OrdinalIgnoreCase));
        if (language == null)
        {
            // Language not found - fetch name and direction from /en/languages API
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

    /// <summary>
    /// Fetches language name and direction from JW.org /en/languages endpoint.
    /// Caches the response to avoid multiple API calls. Thread-safe and async-safe implementation.
    /// </summary>
    public async Task<(string? Name, string Direction)> FetchLanguageInfoFromJwOrgLanguagesApi(string languageCode)
    {
        try
        {
            // Thread-safe and async-safe cache loading using Task-based pattern
            Task<JsonDocument>? loadTask = null;
            bool useExistingCache = false;
            
            lock (languagesCacheLock)
            {
                if (languagesCache != null)
                {
                    // Cache already loaded, use it directly
                    useExistingCache = true;
                }
                else if (languagesCacheTask != null)
                {
                    // Another thread is loading, reuse the same task
                    loadTask = languagesCacheTask;
                }
                else
                {
                    // We need to load the cache
                    loadTask = LoadLanguagesCacheAsync();
                    languagesCacheTask = loadTask;
                }
            }

            // Wait for cache to be loaded (either by us or another thread)
            JsonDocument cache;
            if (useExistingCache)
            {
                // Cache was already loaded, use it directly
                lock (languagesCacheLock)
                {
                    cache = languagesCache!; // Safe because we checked it's not null above
                }
            }
            else
            {
                // Wait for the loading task (either ours or another thread's)
                cache = await loadTask!;
                
                // Store in synchronous cache for faster access next time
                lock (languagesCacheLock)
                {
                    if (languagesCache == null)
                    {
                        languagesCache = cache;
                    }
                }
            }

            var root = cache.RootElement;
            
            // Handle both array and object responses
            JsonElement languagesArray;
            if (root.ValueKind == JsonValueKind.Array)
            {
                languagesArray = root;
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                // Try common property names that might contain the languages array
                if (root.TryGetProperty(AppConstants.Media.LanguageIndexJson.Languages, out var languagesProp) && languagesProp.ValueKind == JsonValueKind.Array)
                {
                    languagesArray = languagesProp;
                }
                else if (root.TryGetProperty(AppConstants.Media.LanguageIndexJson.Data, out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
                {
                    languagesArray = dataProp;
                }
                else
                {
                    // Log available properties for debugging
                    var properties = root.EnumerateObject().Select(p => p.Name).ToList();
                    logger.Warning("Expected JSON array or object with 'languages'/'data' array from /en/languages endpoint. Got object with properties: {Properties}", string.Join(", ", properties));
                    return (null, AppConstants.Media.TextDirectionLeftToRight);
                }
            }
            else
            {
                logger.Warning("Expected JSON array or object from /en/languages endpoint, got {ValueKind}", root.ValueKind);
                return (null, AppConstants.Media.TextDirectionLeftToRight);
            }

            // Search for the language by langcode
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

                // Found the language - extract name and direction
                var name = languageCode; // Default to code if name not found
                if (langElement.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var nameElement))
                {
                    var rawName = nameElement.GetString();
                    if (!string.IsNullOrWhiteSpace(rawName))
                    {
                        name = MediaTrackTitleHelper.DecodeHtmlTitle(rawName);
                        // Truncate if too long (max 100 characters)
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

            // Language not found in the API response
            logger.Debug("Language {LanguageCode} not found in /en/languages API response", languageCode);
            return (null, AppConstants.Media.TextDirectionLeftToRight);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to fetch language info for {LanguageCode} from /en/languages API, using default ltr", languageCode);
            return (null, AppConstants.Media.TextDirectionLeftToRight);
        }
    }

    /// <summary>
    /// Loads the languages cache from JW.org /en/languages endpoint.
    /// This method is called once and the result is cached and reused.
    /// </summary>
    private async Task<JsonDocument> LoadLanguagesCacheAsync()
    {
        logger.Debug("Fetching languages from JW.org /en/languages endpoint for lookup...");
        var url = AppConstants.ApiEndpoints.JwOrgLanguagesListUrl;
        var jsonString = await downloadUtility.GetAsync(url);
        return JsonDocument.Parse(jsonString);
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
            // Load the languages cache (this will fetch from API if not already cached)
            var cache = await LoadLanguagesCacheAsync();
            var root = cache.RootElement;

            // Handle both array and object responses
            JsonElement languagesArray;
            if (root.ValueKind == JsonValueKind.Array)
            {
                languagesArray = root;
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                // Try common property names that might contain the languages array
                if (root.TryGetProperty(AppConstants.Media.LanguageIndexJson.Languages, out var languagesProp) && languagesProp.ValueKind == JsonValueKind.Array)
                {
                    languagesArray = languagesProp;
                }
                else if (root.TryGetProperty(AppConstants.Media.LanguageIndexJson.Data, out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
                {
                    languagesArray = dataProp;
                }
                else
                {
                    logger.Warning("Expected JSON array or object with 'languages'/'data' array from /en/languages endpoint");
                    return;
                }
            }
            else
            {
                logger.Warning("Expected JSON array or object from /en/languages endpoint, got {ValueKind}", root.ValueKind);
                return;
            }

            var languagesSeeded = 0;
            var languagesSkipped = 0;
            var signLanguagesSkipped = 0;

            // Process all languages from the API response
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

                // Check if this is a sign language
                var isSignLanguage = false;
                if (langElement.TryGetProperty(AppConstants.Media.LanguageIndexJson.IsSignLanguage, out var isSignLanguageElement))
                {
                    isSignLanguage = isSignLanguageElement.GetBoolean();
                }

                if (isSignLanguage)
                {
                    signLanguagesSkipped++;
                    logger.Debug("Skipping sign language: {LanguageCode}", langcode);
                    continue;
                }

                var normalizedCode = langcode.ToUpperInvariant();

                // Check if language already exists
                var existingLanguage = await db.Languages
                    .FirstOrDefaultAsync(l => l.LanguageCode == normalizedCode);

                if (existingLanguage != null)
                {
                    languagesSkipped++;
                    continue;
                }

                // Extract name and direction
                var name = normalizedCode; // Default to code if name not found
                if (langElement.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var nameElement))
                {
                    var rawName = nameElement.GetString();
                    if (!string.IsNullOrWhiteSpace(rawName))
                    {
                        name = MediaTrackTitleHelper.DecodeHtmlTitle(rawName);
                        // Truncate if too long (max 100 characters)
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

                // Create and add language (name goes to LanguageNamesByLanguage for "E")
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
                languagesSeeded++;
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
