#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Bible.Alarm.AudioLinksHarvestor.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media;
using BiblePublication = Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublication;
using Language = Bible.Alarm.Shared.Models.Media.Language;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using BiblePublicationSection = Bible.Alarm.AudioLinksHarvestor.Models.BiblePublications.BiblePublicationSection;
using BiblePublicationTrack = Bible.Alarm.AudioLinksHarvestor.Models.BiblePublications.BiblePublicationTrack;
using DramaTrack = Bible.Alarm.AudioLinksHarvestor.Models.Drama.DramaTrack;
using MusicTrack = Bible.Alarm.AudioLinksHarvestor.Models.Music.MusicTrack;
using Publication = Bible.Alarm.AudioLinksHarvestor.Models.Publication;
using VideoEpisode = Bible.Alarm.AudioLinksHarvestor.Models.Video.VideoEpisode;

namespace Bible.Alarm.AudioLinksHarvestor.Utility;

internal class DbSeeder : IDataPersister
{
    private readonly ILogger logger;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly DownloadUtility downloadUtility;
    private readonly InMemoryDataStore dataStore;
    private bool isTestRun;
    private JsonDocument? languagesCache; // Cache for /en/languages API response
    private readonly object languagesCacheLock = new object(); // Lock for thread-safe cache access
    private Task<JsonDocument>? languagesCacheTask; // Task for async-safe cache loading

    /// <summary>
    /// Exposes the PublicationLanguages data store for access by harvesters after discovery phase.
    /// </summary>
    public IReadOnlyDictionary<string, Dictionary<string, LanguageInfo>> PublicationLanguages => dataStore.PublicationLanguages;

    public DbSeeder(ILogger logger, IServiceScopeFactory scopeFactory, DownloadUtility downloadUtility, bool isTestRun = false)
    {
        this.logger = logger;
        this.scopeFactory = scopeFactory;
        this.downloadUtility = downloadUtility;
        this.dataStore = new InMemoryDataStore();
        this.isTestRun = isTestRun;
    }

    public async Task Seed()
    {
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = DELETE;");
            
            logger.Information("Applying database migrations...");
            try
            {
                // MigrateAsync will create the database if it doesn't exist
                await db.Database.MigrateAsync();
                
                // Verify the database was created and migrations were applied
                var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    logger.Warning("Warning: {Count} pending migrations found after migration", pendingMigrations.Count());
                }
                
                var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
                logger.Information("Applied {Count} migrations successfully", appliedMigrations.Count());
                
                // Verify that Categories table exists
                var canConnect = await db.Database.CanConnectAsync();
                if (!canConnect)
                {
                    throw new InvalidOperationException("Cannot connect to database after migration");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to apply migrations");
                throw;
            }
        }

        // Seed default Categories and ApiUrls first
        await SeedDefaultCategoriesAndApiUrls();
        
        // Seed ALL languages from jw.org /en/languages API to Languages table
        await SeedAllLanguagesFromJwOrg();
        
        // Seed discovered languages for on-demand fetching (discovery tables)
        // Note: Only seed PublicationLanguages here - SectionLanguages needs English publications to exist first
        await SeedPublicationLanguages();
        
        // Seed English using shared FetchAndSave* methods (same as used for other languages in test mode)
        // This happens after discovery tables are seeded, using the same methods that are used for on-demand fetching
        await SeedEnglish();
        
        // Now seed SectionLanguages after English publications exist (needed for section lookup)
        await SeedSectionLanguages();
        
        // In test mode, also seed MY and A after English
        if (isTestRun)
        {
            await SeedTestLanguages();
        }
    }

    /// <summary>
    /// Seeds test languages (MY and A) for all discovered publications in test mode.
    /// Uses EnsurePublicationExistsAsync to seed each publication for each test language.
    /// </summary>
    private async Task SeedTestLanguages()
    {
        using var scope = scopeFactory.CreateScope();
        var httpClient = scope.ServiceProvider.GetRequiredService<System.Net.Http.HttpClient>();
        var languageContentService = new LanguageContentService(scopeFactory, logger, httpClient);

        var testLanguages = new[] { "MY", "A" };
        logger.Information("=== Seeding test languages ({Languages}) for all discovered publications ===",
            string.Join(", ", testLanguages));

        // Get all discovered publication codes from PublicationLanguages
        var publicationCodes = dataStore.PublicationLanguages.Keys.ToList();

        // Note: "iam" (Kingdom Melodies) is not included here because it doesn't support ad-hoc fetching
        // for other languages - it's only seeded once with null language for English

        if (publicationCodes.Count == 0)
        {
            logger.Warning("No publications discovered, skipping test language seeding");
            return;
        }

        logger.Information("Found {Count} publication(s) to seed test languages for", publicationCodes.Count);

        foreach (var languageCode in testLanguages)
        {
            logger.Information("=== Seeding {LanguageCode} for all discovered publications ===", languageCode);

            foreach (var publicationCode in publicationCodes.OrderBy(pc => pc))
            {
                logger.Information("Seeding {LanguageCode} for publication: {PublicationCode}", languageCode, publicationCode);

                // Use EnsurePublicationExistsAsync to seed the publication for this language
                // This will fetch the publication if it doesn't exist
                var success = await languageContentService.EnsurePublicationExistsAsync(publicationCode, languageCode);

                if (success)
                {
                    logger.Information("✓ Successfully seeded {LanguageCode} for publication {PublicationCode}", languageCode, publicationCode);
                }
                else
                {
                    logger.Warning("✗ Failed to seed {LanguageCode} for publication {PublicationCode}", languageCode, publicationCode);
                }
            }

            logger.Information("=== {LanguageCode} seeding completed ===", languageCode);
        }

        logger.Information("=== Test language seeding completed ===");
    }

    private async Task SeedDefaultCategoriesAndApiUrls()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        // Seed Categories
        var categories = new[]
        {
            new Category { CategoryName = "Bible" },
            new Category { CategoryName = "Dramas" },
            new Category { CategoryName = "Music" }
        };

        foreach (var category in categories)
        {
            var existing = await db.Categories.FirstOrDefaultAsync(c => c.CategoryName == category.CategoryName);
            if (existing == null)
            {
                db.Categories.Add(category);
                logger.Information("Seeding category: {CategoryName}", category.CategoryName);
            }
        }

        // Seed ApiUrls
        var apiUrls = new[]
        {
            new BaseUrl
            {
                Url = "https://b.jw-cdn.org",
                PathPrefix = "apis/pub-media/GETPUBMEDIALINKS"
            },
            new BaseUrl
            {
                Url = "https://app.jw-cdn.org",
                PathPrefix = "apis/pub-media/GETPUBMEDIALINKS"
            }
        };

        foreach (var apiUrl in apiUrls)
        {
            // Check if this specific URL and PathPrefix combination already exists
            var existingApiUrl = await db.BaseUrls.FirstOrDefaultAsync(a => 
                a.Url == apiUrl.Url && a.PathPrefix == apiUrl.PathPrefix);
            if (existingApiUrl == null)
            {
                db.BaseUrls.Add(apiUrl);
                await db.SaveChangesAsync(); // Save to get the ID
                
                // Seed UrlParam with output=json for this base URL
                var urlParam = new UrlParam
                {
                    BaseUrlId = apiUrl.Id,
                    Key = "output",
                    Value = "json",
                    IsQueryParam = true
                };
                db.UrlParams.Add(urlParam);
                
                logger.Information("Seeding ApiUrl: {BaseUrl} / {PathPrefix} with output=json", apiUrl.Url, apiUrl.PathPrefix);
            }
            else
            {
                // Check if output=json param already exists for this base URL
                var existingParam = await db.UrlParams.FirstOrDefaultAsync(p => 
                    p.BaseUrlId == existingApiUrl.Id && p.Key == "output" && p.Value == "json");
                if (existingParam == null)
                {
                    var urlParam = new UrlParam
                    {
                        BaseUrlId = existingApiUrl.Id,
                        Key = "output",
                        Value = "json",
                        IsQueryParam = true
                    };
                    db.UrlParams.Add(urlParam);
                    logger.Information("Adding output=json param to existing ApiUrl: {BaseUrl} / {PathPrefix}", existingApiUrl.Url, existingApiUrl.PathPrefix);
                }
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Gets an existing language by code (case-insensitive) or creates one by looking it up from /en/languages API.
    /// </summary>
    private async Task<Language> GetOrCreateLanguageByCode(MediaDbContext db, string code)
    {
        // Normalize code to uppercase for consistent storage and comparison
        var normalizedCode = code.ToUpperInvariant();

        // Case-insensitive lookup by code only
        var language = await db.Languages.FirstOrDefaultAsync(x => x.LanguageCode.ToUpper() == normalizedCode);
        if (language == null)
        {
            // Language not found - fetch name and direction from /en/languages API
            var (name, direction) = await FetchLanguageInfoFromJwOrgLanguagesApi(normalizedCode);
            
            language = new Language
            {
                LanguageCode = normalizedCode,
                Name = name ?? normalizedCode, // Use fetched name or code as fallback
                Direction = direction
            };
            db.Languages.Add(language);
            await db.SaveChangesAsync();
            
            logger.Information("Created new language {Code}: Name={Name}, Direction={Direction} from /en/languages API", 
                normalizedCode, name ?? normalizedCode, direction);
        }
        return language;
    }

    /// <summary>
    /// Fetches language name and direction from JW.org /en/languages endpoint.
    /// Caches the response to avoid multiple API calls. Thread-safe and async-safe implementation.
    /// </summary>
    private async Task<(string? Name, string Direction)> FetchLanguageInfoFromJwOrgLanguagesApi(string languageCode)
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
                if (root.TryGetProperty("languages", out var languagesProp) && languagesProp.ValueKind == JsonValueKind.Array)
                {
                    languagesArray = languagesProp;
                }
                else if (root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
                {
                    languagesArray = dataProp;
                }
                else
                {
                    // Log available properties for debugging
                    var properties = root.EnumerateObject().Select(p => p.Name).ToList();
                    logger.Warning("Expected JSON array or object with 'languages'/'data' array from /en/languages endpoint. Got object with properties: {Properties}", string.Join(", ", properties));
                    return (null, "ltr");
                }
            }
            else
            {
                logger.Warning("Expected JSON array or object from /en/languages endpoint, got {ValueKind}", root.ValueKind);
                return (null, "ltr");
            }

            // Search for the language by langcode
            foreach (var langElement in languagesArray.EnumerateArray())
            {
                if (!langElement.TryGetProperty("langcode", out var langcodeElement))
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
                if (langElement.TryGetProperty("name", out var nameElement))
                {
                    var rawName = nameElement.GetString();
                    if (!string.IsNullOrWhiteSpace(rawName))
                    {
                        name = WebUtility.HtmlDecode(rawName);
                        // Truncate if too long (max 100 characters)
                        if (name.Length > 100)
                        {
                            name = name.Substring(0, 100);
                        }
                    }
                }

                var direction = "ltr";
                if (langElement.TryGetProperty("direction", out var directionElement))
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
            return (null, "ltr");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to fetch language info for {LanguageCode} from /en/languages API, using default ltr", languageCode);
            return (null, "ltr");
        }
    }

    /// <summary>
    /// Loads the languages cache from JW.org /en/languages endpoint.
    /// This method is called once and the result is cached and reused.
    /// </summary>
    private async Task<JsonDocument> LoadLanguagesCacheAsync()
    {
        logger.Debug("Fetching languages from JW.org /en/languages endpoint for lookup...");
        var url = "https://www.jw.org/en/languages";
        var jsonString = await downloadUtility.GetAsync(url);
        return JsonDocument.Parse(jsonString);
    }

    /// <summary>
    /// Seeds ALL languages from jw.org /en/languages API to the Languages table.
    /// This ensures all languages are available in the database, not just discovered ones.
    /// </summary>
    private async Task SeedAllLanguagesFromJwOrg()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        logger.Information("=== Seeding all languages from jw.org /en/languages API ===");

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
                if (root.TryGetProperty("languages", out var languagesProp) && languagesProp.ValueKind == JsonValueKind.Array)
                {
                    languagesArray = languagesProp;
                }
                else if (root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
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

            // Process all languages from the API response
            foreach (var langElement in languagesArray.EnumerateArray())
            {
                if (!langElement.TryGetProperty("langcode", out var langcodeElement))
                {
                    continue;
                }

                var langcode = langcodeElement.GetString();
                if (string.IsNullOrWhiteSpace(langcode))
                {
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
                if (langElement.TryGetProperty("name", out var nameElement))
                {
                    var rawName = nameElement.GetString();
                    if (!string.IsNullOrWhiteSpace(rawName))
                    {
                        name = WebUtility.HtmlDecode(rawName);
                        // Truncate if too long (max 100 characters)
                        if (name.Length > 100)
                        {
                            name = name.Substring(0, 100);
                        }
                    }
                }

                var direction = "ltr";
                if (langElement.TryGetProperty("direction", out var directionElement))
                {
                    var dirValue = directionElement.GetString();
                    if (!string.IsNullOrWhiteSpace(dirValue))
                    {
                        direction = dirValue;
                    }
                }

                // Create and add language
                var language = new Language
                {
                    LanguageCode = normalizedCode,
                    Name = name,
                    Direction = direction
                };
                db.Languages.Add(language);
                languagesSeeded++;
            }

            await db.SaveChangesAsync();

            logger.Information("✓ Seeded {SeededCount} languages from jw.org API ({SkippedCount} already existed)",
                languagesSeeded, languagesSkipped);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to seed all languages from jw.org API");
            throw;
        }
    }

    /// <summary>
    /// Fetches language name and direction from the Mediator API.
    /// Uses the Dramas category as it's commonly available across languages.
    /// This is kept as a fallback but /en/languages API is preferred.
    /// </summary>
    private async Task<(string? Name, string Direction)> FetchLanguageInfoFromApi(string languageCode)
    {
        try
        {
            // Use Dramas category to fetch language info
            var url = $"{AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrl}/categories/{languageCode}/Dramas?detailed=1";
            var jsonString = await downloadUtility.GetAsync(url);

            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.TryGetProperty("language", out var langElement))
            {
                var direction = "ltr";
                if (langElement.TryGetProperty("direction", out var dirElement))
                {
                    direction = dirElement.GetString() ?? "ltr";
                }

                string? name = null;
                if (langElement.TryGetProperty("name", out var nameElement))
                {
                    var rawName = nameElement.GetString();
                    // Decode HTML entities like &nbsp; to proper characters
                    name = rawName != null ? WebUtility.HtmlDecode(rawName) : null;
                }

                return (name, direction);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to fetch language info for {LanguageCode} from API, using default ltr", languageCode);
        }

        return (null, "ltr");
    }

    private async Task<T?> GetSafely<T>(Func<Task<T>> getter, string? context = null) where T : class
    {
        try
        {
            return await getter();
        }
        catch (FileNotFoundException ex)
        {
            if (!string.IsNullOrEmpty(context))
            {
                logger.Warning("File not found for {Context}: {FileName}", context, ex.FileName);
            }
            return null;
        }
        catch (DirectoryNotFoundException ex)
        {
            if (!string.IsNullOrEmpty(context))
            {
                logger.Warning("Directory not found for {Context}: {DirectoryName}", context, ex.Message);
            }
            return null;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(context))
            {
                logger.Error(ex, "Error in {Context}", context);
            }
            return null;
        }
    }


    private async Task<List<BaseUrl>> GetAllBaseUrls(MediaDbContext db)
    {
        return await db.BaseUrls
            .Where(x => x.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
            .ToListAsync();
    }

    private async Task<Category> GetCategory(MediaDbContext db, string categoryName)
    {
        return await db.Categories.FirstAsync(x => x.CategoryName == categoryName);
    }

    private async Task<BiblePublication> CreateBiblePublication(
        MediaDbContext db,
        Publication publication, 
        Language? newLanguage,
        int categoryId,
        string? languageCode,
        bool isVideo = false)
    {
        // Create UrlParam entries for BiblePublication
        var urlParams = new List<UrlParam>
        {
            new UrlParam
            {
                BiblePublicationId = 0, // Will be set after publication is saved
                Key = "pub",
                Value = publication.Code,
                IsQueryParam = true
            },
            new UrlParam
            {
                BiblePublicationId = 0, // Will be set after publication is saved
                Key = "fileformat",
                Value = isVideo ? "mp4" : "mp3",
                IsQueryParam = true
            }
        };

        // Add langwritten parameter only if language is provided (vocals have language, melodies don't)
        if (!string.IsNullOrEmpty(languageCode))
        {
            urlParams.Add(new UrlParam
            {
                BiblePublicationId = 0, // Will be set after publication is saved
                Key = "langwritten",
                Value = languageCode,
                IsQueryParam = true
            });
        }

        // Normalize code to lowercase for consistency
        var normalizedCode = publication.Code.ToLowerInvariant();
        
        var biblePublication = new BiblePublication
        {
            Name = publication.Name,
            PublicationCode = normalizedCode,
            Language = newLanguage, // Optional - can be null
            CategoryId = categoryId,
            UrlParams = urlParams, // Optional - can be empty
            IsVideo = isVideo
        };

        return biblePublication;
    }



    #region IDataPersister Implementation

    public Task SaveBiblePublicationSections(
        string languageCode,
        string publicationCode,
        string publicationName,
        Dictionary<int, BiblePublicationSection> sections,
        Dictionary<int, Dictionary<int, BiblePublicationTrack>> sectionNumberTrackMap)
    {
        var key = (languageCode.ToUpperInvariant(), publicationCode.ToLowerInvariant());
        dataStore.BiblePublications[key] = (publicationName, sections, sectionNumberTrackMap);
        return Task.CompletedTask;
    }

    public Task SaveDramaPublication(
        string languageCode,
        string publicationCode,
        string publicationName,
        Dictionary<string, List<DramaTrack>> tracksBySection,
        Dictionary<string, string> sectionNames)
    {
        var key = (languageCode.ToUpperInvariant(), publicationCode.ToLowerInvariant());
        dataStore.DramaPublications[key] = (publicationName, tracksBySection, sectionNames);
        return Task.CompletedTask;
    }

    public Task SaveMusicTracks(
        string publicationCode,
        string? languageCode,
        string publicationName,
        List<MusicTrack> tracks)
    {
        var key = (publicationCode.ToLowerInvariant(), languageCode?.ToUpperInvariant());
        dataStore.MusicTracks[key] = (publicationName, tracks);
        return Task.CompletedTask;
    }

    public Task SaveMelodyMusicTracks(
        string publicationCode,
        Dictionary<string, List<MusicTrack>> discTracksMap,
        Dictionary<string, string> discNamesMap)
    {
        dataStore.MelodyMusic[publicationCode.ToLowerInvariant()] = (discTracksMap, discNamesMap);
        return Task.CompletedTask;
    }

    public Task SaveVideoEpisodes(
        string languageCode,
        string publicationCode,
        string publicationName,
        List<VideoEpisode> episodes)
    {
        var key = (languageCode.ToUpperInvariant(), publicationCode.ToLowerInvariant());
        dataStore.VideoPublications[key] = (publicationName, episodes);
        return Task.CompletedTask;
    }

    public Task SaveLanguageDiscovery(
        string languageCode,
        string publicationCode,
        Dictionary<string, string> languageCodeToNameMapping)
    {
        var key = (languageCode.ToUpperInvariant(), publicationCode.ToLowerInvariant());
        dataStore.LanguageDiscovery[key] = languageCodeToNameMapping;
        return Task.CompletedTask;
    }

    public Task SavePublicationLanguages(
        string publicationCode,
        Dictionary<string, LanguageInfo> discoveredLanguages)
    {
        // Save ALL discovered languages (including English) - English will be seeded separately but should be tracked
        // Test mode filtering only applies to which languages get seeded (content downloaded), not which languages get saved to discovery tables
        var languagesToSave = discoveredLanguages;
        
        if (languagesToSave.Count > 0)
        {
            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            // Merge with existing languages for this publication (don't overwrite, merge)
            if (dataStore.PublicationLanguages.TryGetValue(normalizedPublicationCode, out var existingLanguages))
            {
                foreach (var lang in languagesToSave)
                {
                    existingLanguages[lang.Key] = lang.Value;
                }
            }
            else
            {
                dataStore.PublicationLanguages[normalizedPublicationCode] = languagesToSave;
            }
        }
        return Task.CompletedTask;
    }

    public Task SaveSectionLanguages(
        string publicationCode,
        string sectionCode,
        Dictionary<string, LanguageInfo> discoveredLanguages)
    {
        // Save ALL discovered languages (including English) - English will be seeded separately but should be tracked
        // Test mode filtering only applies to which languages get seeded (content downloaded), not which languages get saved to discovery tables
        var languagesToSave = discoveredLanguages;
        
        if (languagesToSave.Count > 0)
        {
            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            var normalizedSectionCode = sectionCode.ToLowerInvariant();
            var key = (normalizedPublicationCode, normalizedSectionCode);
            // Merge with existing languages for this section (don't overwrite, merge)
            if (dataStore.SectionLanguages.TryGetValue(key, out var existingLanguages))
            {
                foreach (var lang in languagesToSave)
                {
                    existingLanguages[lang.Key] = lang.Value;
                }
            }
            else
            {
                dataStore.SectionLanguages[key] = languagesToSave;
            }
        }
        return Task.CompletedTask;
    }

    #endregion

    #region Seed Discovered Languages

    /// <summary>
    /// Seeds English (E) for all discovered publications using the shared FetchAndSave* methods.
    /// This is the same approach used for other languages in test mode.
    /// </summary>
    private async Task SeedEnglish()
    {
        using var scope = scopeFactory.CreateScope();
        var languageContentService = scope.ServiceProvider.GetRequiredService<LanguageContentService>();

        logger.Information("=== Seeding English (E) for all discovered publications ===");

        // Get all discovered publication codes from PublicationLanguages (where English is available)
        var publicationCodes = dataStore.PublicationLanguages.Keys.ToList();

        // Add "iam" (Kingdom Melodies) explicitly since it's melody music without language discovery
        // but still needs to be seeded for English
        if (!publicationCodes.Contains("iam", StringComparer.OrdinalIgnoreCase))
        {
            publicationCodes.Add("iam");
        }

        if (publicationCodes.Count == 0)
        {
            logger.Warning("No publications discovered, skipping English seeding");
            return;
        }

        logger.Information("Found {Count} publication(s) to seed English for", publicationCodes.Count);

        foreach (var publicationCode in publicationCodes.OrderBy(pc => pc))
        {
            logger.Information("Seeding English for publication: {PublicationCode}", publicationCode);

            // Use shared LanguageContentService to seed English publication
            // This reuses the same code used for ad-hoc fetching
            var success = await languageContentService.SeedEnglishPublicationAsync(publicationCode);

            if (success)
            {
                logger.Information("✓ Successfully seeded English for publication {PublicationCode}", publicationCode);
            }
            else
            {
                logger.Warning("✗ Failed to seed English for publication {PublicationCode}", publicationCode);
            }
        }

        logger.Information("=== English seeding completed ===");
    }

    private async Task SeedPublicationLanguages()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        await SeedPublicationLanguagesInternal(db);
    }

    private async Task SeedSectionLanguages()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        await SeedSectionLanguagesInternal(db);
    }

    private async Task SeedPublicationLanguagesInternal(MediaDbContext db)
    {
        // Get all distinct languages discovered across all publications
        var allDiscoveredLanguageCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Add all languages from PublicationLanguages
        foreach (var (publicationCode, languages) in dataStore.PublicationLanguages)
        {
            foreach (var languageCode in languages.Keys)
            {
                allDiscoveredLanguageCodes.Add(languageCode);
            }
        }

        // Add all languages from SectionLanguages
        foreach (var ((publicationCode, sectionNumber), languages) in dataStore.SectionLanguages)
        {
            foreach (var languageCode in languages.Keys)
            {
                allDiscoveredLanguageCodes.Add(languageCode);
            }
        }

        // Always include English (E) since we seed it
        allDiscoveredLanguageCodes.Add("E");

        // Get or create all discovered languages
        foreach (var languageCode in allDiscoveredLanguageCodes)
        {
            await GetOrCreateLanguageByCode(db, languageCode);
        }

        // First, always seed E for all English publications
        await SeedEnglishForAllPublications(db);
        await db.SaveChangesAsync(); // Save E entries first to avoid duplicates

        // Seed publication languages
        if (dataStore.PublicationLanguages.Count == 0)
        {
            logger.Information("Seeded English (E) for all publications");
            return;
        }

        logger.Information("Seeding discovered languages for {Count} publication(s)", dataStore.PublicationLanguages.Count);

        foreach (var (publicationCode, languages) in dataStore.PublicationLanguages)
        {
            // Pass the original publicationCode (from dataStore) so SeedLanguageForPublication can determine case-sensitive code
            // Seed discovered languages (including E - it should be in the table)
            // Note: We don't need English publication to exist yet - we're just populating the discovery table
            foreach (var (languageCode, languageInfo) in languages)
            {
                await SeedLanguageForPublication(db, publicationCode, languageCode);
            }
        }

        await db.SaveChangesAsync();
        logger.Information("Seeded publication languages");
    }

    private async Task SeedLanguageForPublication(MediaDbContext db, string publicationCode, string languageCode)
    {
        var normalizedPublicationCode = publicationCode.ToLowerInvariant();
        var normalizedLanguageCode = languageCode.ToUpperInvariant();

        // For dramas, use case-sensitive publication codes: "Dramas" or "DramaticBibleReadings"
        var isDrama = PublicationTypeHelper.IsDrama(normalizedPublicationCode);
        string publicationCodeForDb;
        if (isDrama)
        {
            publicationCodeForDb = normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                ? "Dramas"
                : "DramaticBibleReadings";
        }
        else
        {
            publicationCodeForDb = normalizedPublicationCode;
        }

        // Get or create language
        var language = await GetOrCreateLanguageByCode(db, normalizedLanguageCode);
        
        // Determine harvest type and category based on publication code
        var harvestType = PublicationTypeHelper.GetHarvestType(normalizedPublicationCode);
        var categoryName = JwSourceHelper.GetCategoryName(normalizedPublicationCode);
        
        if (string.IsNullOrEmpty(categoryName))
        {
            logger.Warning("Category not found for publication code {PublicationCode}, defaulting to 'Bible'", publicationCode);
            categoryName = "Bible";
        }

        var category = await db.Categories.FirstOrDefaultAsync(c => c.CategoryName == categoryName);
        if (category == null)
        {
            logger.Warning("Category '{CategoryName}' not found in database for publication {PublicationCode}", categoryName, publicationCode);
            return;
        }
        
        // Check if already exists (use case-sensitive code for dramas)
        var exists = await db.PublicationLanguages
            .AnyAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == language.Id);

        if (!exists)
        {
            var publicationLanguage = new Shared.Models.Media.BiblePublications.PublicationLanguage
            {
                PublicationCode = publicationCodeForDb, // Use case-sensitive code for dramas
                Language = language,
                HarvestType = harvestType,
                Category = category,
                CategoryId = category.Id
            };
            db.PublicationLanguages.Add(publicationLanguage);
        }
        else
        {
            // Update existing entry with harvest type and category if missing
            var existing = await db.PublicationLanguages
                .FirstOrDefaultAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == language.Id);
            
            if (existing != null)
            {
                existing.HarvestType = harvestType;
                existing.Category = category;
                existing.CategoryId = category.Id;
            }
        }
    }

    private async Task SeedEnglishForAllPublications(MediaDbContext db)
    {
        // Get all English publications
        var englishPublications = await db.BiblePublications
            .Include(bp => bp.Language)
            .Where(bp => bp.Language != null && bp.Language.LanguageCode == "E")
            .Select(bp => bp.PublicationCode)
            .Distinct()
            .ToListAsync();

        var englishLanguage = await GetOrCreateLanguageByCode(db, "E");

        foreach (var publicationCode in englishPublications)
        {
            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            
            // Determine harvest type and category based on publication code
            var harvestType = PublicationTypeHelper.GetHarvestType(normalizedPublicationCode);
            var categoryName = JwSourceHelper.GetCategoryName(normalizedPublicationCode);
            
            if (string.IsNullOrEmpty(categoryName))
            {
                logger.Warning("Category not found for publication code {PublicationCode}, defaulting to 'Bible'", publicationCode);
                categoryName = "Bible";
            }

            var category = await db.Categories.FirstOrDefaultAsync(c => c.CategoryName == categoryName);
            if (category == null)
            {
                logger.Warning("Category '{CategoryName}' not found in database for publication {PublicationCode}", categoryName, publicationCode);
                continue;
            }
            
            var exists = await db.PublicationLanguages
                .AnyAsync(pl => pl.PublicationCode == normalizedPublicationCode && pl.LanguageId == englishLanguage.Id);

            if (!exists)
            {
                var publicationLanguage = new Shared.Models.Media.BiblePublications.PublicationLanguage
                {
                    PublicationCode = normalizedPublicationCode,
                    Language = englishLanguage,
                    HarvestType = harvestType,
                    Category = category,
                    CategoryId = category.Id
                };
                db.PublicationLanguages.Add(publicationLanguage);
            }
            else
            {
                // Update existing entry with harvest type and category if missing
                var existing = await db.PublicationLanguages
                    .FirstOrDefaultAsync(pl => pl.PublicationCode == normalizedPublicationCode && pl.LanguageId == englishLanguage.Id);
                
                if (existing != null)
                {
                    existing.HarvestType = harvestType;
                    existing.Category = category;
                    existing.CategoryId = category.Id;
                }
            }
        }
    }

    private async Task SeedSectionLanguagesInternal(MediaDbContext db)
    {
        if (dataStore.SectionLanguages.Count == 0)
        {
            return;
        }

        logger.Information("Seeding discovered languages for {Count} section(s)", dataStore.SectionLanguages.Count);

        foreach (var ((publicationCode, sectionCode), languages) in dataStore.SectionLanguages)
        {
            // Normalize to lowercase for lookup, but use case-sensitive code for dramas in database
            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            var normalizedSectionCode = sectionCode.ToLowerInvariant();

            // For dramas, use case-sensitive publication codes: "Dramas" or "DramaticBibleReadings"
            var isDrama = PublicationTypeHelper.IsDrama(normalizedPublicationCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = normalizedPublicationCode;
            }

            // Verify English publication exists (it should be seeded by now, but skip silently if not)
            // Note: We still seed section languages even if English publication doesn't exist,
            // as the discovery phase already found which languages are available for each section
            var englishPublication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.Sections)
                    .ThenInclude(s => s.UrlParams)
                .FirstOrDefaultAsync(bp => bp.PublicationCode == publicationCodeForDb && bp.Language != null && bp.Language.LanguageCode == "E");

            // Try to find section in English publication for reference (but don't require it)
            // Note: englishPublication.Sections returns Shared.Models.Media.BiblePublications.BiblePublicationSection, not the harvester model
            Shared.Models.Media.BiblePublications.BiblePublicationSection? section = null;
            if (englishPublication != null)
            {
                // Find section by SectionCode
                section = englishPublication.Sections.FirstOrDefault(s => s.SectionCode == normalizedSectionCode);
                
                if (section == null)
                {
                    // Try to find by UrlParam booknum or section code as fallback
                    section = englishPublication.Sections.FirstOrDefault(s => 
                        s.UrlParams.Any(up => up.Key.Equals("booknum", StringComparison.OrdinalIgnoreCase) && 
                                             up.Value == normalizedSectionCode) ||
                        s.UrlParams.Any(up => up.Key.Equals("pub", StringComparison.OrdinalIgnoreCase) && 
                                             up.Value.Equals(normalizedSectionCode, StringComparison.OrdinalIgnoreCase)));
                }

                if (section == null)
                {
                    logger.Debug("Section {SectionCode} not found in English publication {PublicationCode}, but seeding discovered languages anyway", 
                        sectionCode, publicationCode);
                }
            }
            else
            {
                logger.Debug("English publication {PublicationCode} not found, but seeding discovered section languages anyway", 
                    publicationCode);
            }

            // Seed ALL discovered languages for this section (including E)
            // The discovery phase already found which languages are available, so we save all of them
            // Pass the original publicationCode (from dataStore) so SeedLanguageForSection can determine case-sensitive code
            foreach (var (languageCode, languageInfo) in languages)
            {
                await SeedLanguageForSection(db, publicationCode, normalizedSectionCode, languageCode);
            }
        }

        await db.SaveChangesAsync();
        logger.Information("Seeded section languages");
    }

    private async Task SeedLanguageForSection(MediaDbContext db, string publicationCode, string sectionCode, string languageCode)
    {
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = publicationCode.ToLowerInvariant();
        var normalizedSectionCode = sectionCode.ToLowerInvariant();

        // For dramas, use case-sensitive publication codes: "Dramas" or "DramaticBibleReadings"
        var isDrama = PublicationTypeHelper.IsDrama(normalizedPublicationCode);
        string publicationCodeForDb;
        if (isDrama)
        {
            publicationCodeForDb = normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                ? "Dramas"
                : "DramaticBibleReadings";
        }
        else
        {
            publicationCodeForDb = normalizedPublicationCode;
        }

        // Get or create language
        var language = await GetOrCreateLanguageByCode(db, normalizedLanguageCode);
        
        // Get the PublicationLanguage for this publication and language
        // Check both in database and in the current context (uncommitted changes)
        var publicationLanguage = await db.PublicationLanguages
            .FirstOrDefaultAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == language.Id);

        if (publicationLanguage == null)
        {
            // Also check if it's being tracked in the context but not yet saved
            publicationLanguage = db.ChangeTracker.Entries<Shared.Models.Media.BiblePublications.PublicationLanguage>()
                .Where(e => e.Entity.PublicationCode == publicationCodeForDb && e.Entity.LanguageId == language.Id)
                .Select(e => e.Entity)
                .FirstOrDefault();

            if (publicationLanguage == null)
            {
                logger.Warning("PublicationLanguage not found for {PublicationCode} and {LanguageCode}, creating it", publicationCodeForDb, languageCode);
                
                // Determine harvest type and category based on publication code
                var harvestType = PublicationTypeHelper.GetHarvestType(normalizedPublicationCode);
                var categoryName = JwSourceHelper.GetCategoryName(normalizedPublicationCode);
                
                if (string.IsNullOrEmpty(categoryName))
                {
                    logger.Warning("Category not found for publication code {PublicationCode}, defaulting to 'Bible'", publicationCode);
                    categoryName = "Bible";
                }

                var category = await db.Categories.FirstOrDefaultAsync(c => c.CategoryName == categoryName);
                if (category == null)
                {
                    logger.Warning("Category '{CategoryName}' not found in database for publication {PublicationCode}", categoryName, publicationCode);
                    return; // Can't create SectionLanguage without PublicationLanguage
                }
                
                publicationLanguage = new Shared.Models.Media.BiblePublications.PublicationLanguage
                {
                    PublicationCode = publicationCodeForDb, // Use case-sensitive code for dramas
                    Language = language,
                    HarvestType = harvestType,
                    Category = category,
                    CategoryId = category.Id
                };
                db.PublicationLanguages.Add(publicationLanguage);
                await db.SaveChangesAsync(); // Save to get the ID
            }
        }
        
        // Check if already exists (use case-sensitive code for dramas)
        var exists = await db.SectionLanguages
            .AnyAsync(sl => sl.PublicationCode == publicationCodeForDb && 
                           sl.SectionCode == normalizedSectionCode && 
                           sl.LanguageId == language.Id);

        if (!exists)
        {
            var sectionLanguage = new Shared.Models.Media.BiblePublications.SectionLanguage
            {
                PublicationCode = publicationCodeForDb, // Use case-sensitive code for dramas
                SectionCode = normalizedSectionCode,
                Language = language,
                PublicationLanguage = publicationLanguage,
                HarvestType = publicationLanguage.HarvestType
            };
            db.SectionLanguages.Add(sectionLanguage);
        }
    }

    #endregion

    #region Test Mode On-Demand Fetching

    /// <summary>
    /// Tests on-demand fetching of non-English languages (MY and A) for all English publications.
    /// Uses LanguageContentService from Shared project with data-driven approach.
    /// Only runs in test mode to validate the on-demand fetching logic.
    /// </summary>
    public async Task TestOnDemandFetching()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var httpClient = scope.ServiceProvider.GetRequiredService<System.Net.Http.HttpClient>();
        var languageContentService = new LanguageContentService(scopeFactory, logger, httpClient);

        // Test languages: Malayalam (MY) and Arabic (A)
        var testLanguages = new[] { "MY", "A" };

        logger.Information("=== TEST MODE: Testing on-demand fetching for languages {Languages} ===",
            string.Join(", ", testLanguages));

        // Get all publication codes from PublicationLanguages (these are the ones available for non-English)
        var publicationCodes = await db.PublicationLanguages
            .Include(pl => pl.Language)
                .Where(pl => pl.Language.LanguageCode != "E") // Exclude English
            .Select(pl => pl.PublicationCode)
            .Distinct()
            .ToListAsync();

        if (publicationCodes.Count == 0)
        {
            logger.Warning("No publications found in PublicationLanguages for testing");
            return;
        }

        logger.Information("Found {Count} publication(s) to test on-demand fetching", publicationCodes.Count);

        var totalStartTime = DateTime.UtcNow;
        var publicationStats = new List<(string PublicationCode, Dictionary<string, (TimeSpan Total, TimeSpan? Sections, TimeSpan? SectionTracks, int? SectionCount, TimeSpan? PublicationTracks)> LanguageTimes)>();

        foreach (var publicationCode in publicationCodes.OrderBy(pc => pc))
        {
            var languageTimes = new Dictionary<string, (TimeSpan Total, TimeSpan? Sections, TimeSpan? SectionTracks, int? SectionCount, TimeSpan? PublicationTracks)>();
            var normalizedPublicationCode = publicationCode.ToLowerInvariant();

            // For dramas, use case-sensitive publication codes: "Dramas" or "DramaticBibleReadings"
            var isDrama = PublicationTypeHelper.IsDrama(normalizedPublicationCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = normalizedPublicationCode;
            }

            // Get English publication to determine category/harvester type
            var englishPublication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.Category)
                .FirstOrDefaultAsync(bp => bp.PublicationCode == publicationCodeForDb &&
                                          bp.Language != null &&
                                          bp.Language.LanguageCode == "E");

            if (englishPublication == null)
            {
                logger.Warning("English publication {PublicationCode} not found, skipping", publicationCode);
                continue;
            }

            logger.Information("Testing on-demand fetching for publication: {PublicationCode} (Category: {Category}, IsVideo: {IsVideo})",
                publicationCode, englishPublication.Category?.CategoryName ?? "Unknown", englishPublication.IsVideo);

            foreach (var testLanguageCode in testLanguages)
            {
                var normalizedTestLanguageCode = testLanguageCode.ToUpperInvariant();
                
                // Check if language is available for this publication
                var isAvailable = await db.PublicationLanguages
                    .Include(pl => pl.Language)
                    .AnyAsync(pl => pl.PublicationCode == normalizedPublicationCode &&
                                   pl.Language.LanguageCode == normalizedTestLanguageCode);

                if (!isAvailable)
                {
                    logger.Warning("Language {LanguageCode} is not available for publication {PublicationCode}, skipping",
                        testLanguageCode, publicationCode);
                    continue;
                }

                // Check if publication already exists for this language
                var existing = await db.BiblePublications
                    .Include(bp => bp.Language)
                    .AnyAsync(bp => bp.PublicationCode == normalizedPublicationCode &&
                                  bp.Language != null &&
                                  bp.Language.LanguageCode == normalizedTestLanguageCode);

                if (existing)
                {
                    logger.Information("Publication {PublicationCode} for language {LanguageCode} already exists, skipping fetch",
                        publicationCode, testLanguageCode);
                    continue;
                }

                var languageStartTime = DateTime.UtcNow;
                try
                {
                    // Check if publication has sections by checking SectionLanguages table for English
                    // (SectionLanguages is only populated for English during initial harvest)
                    var hasSections = await db.SectionLanguages
                        .Include(sl => sl.Language)
                        .AnyAsync(sl => sl.PublicationCode == normalizedPublicationCode &&
                                      sl.Language.LanguageCode == "E");

                    bool success;
                    if (hasSections)
                    {
                        // Publication has sections: Fetch all sections first, then tracks for each section
                        logger.Information("Fetching sections for publication {PublicationCode} in language {LanguageCode}...",
                            publicationCode, testLanguageCode);

                        var sectionsStartTime = DateTime.UtcNow;
                        success = await languageContentService.FetchPublicationSectionsAsync(
                            normalizedPublicationCode, normalizedTestLanguageCode);
                        var sectionsElapsed = DateTime.UtcNow - sectionsStartTime;

                        if (success)
                        {
                            logger.Information("✓ Fetched sections for {PublicationCode} in {LanguageCode} in {ElapsedMs}ms",
                                publicationCode, testLanguageCode, sectionsElapsed.TotalMilliseconds);

                            // Get all section codes for this publication from English (E)
                            // (SectionLanguages is only populated for English during initial harvest)
                            var sectionCodes = await db.SectionLanguages
                                .Include(sl => sl.Language)
                                .Where(sl => sl.PublicationCode == normalizedPublicationCode &&
                                           sl.Language.LanguageCode == "E")
                                .Select(sl => sl.SectionCode)
                                .Distinct()
                                .OrderBy(sc => sc)
                                .ToListAsync();

                            logger.Information("Fetching tracks for {Count} section(s) in publication {PublicationCode} for language {LanguageCode}...",
                                sectionCodes.Count, publicationCode, testLanguageCode);

                            var tracksStartTime = DateTime.UtcNow;
                            var sectionsFetched = 0;
                            foreach (var sectionCode in sectionCodes)
                            {
                                var sectionSuccess = await languageContentService.FetchSectionTracksAsync(
                                    normalizedPublicationCode, sectionCode, normalizedTestLanguageCode);
                                if (sectionSuccess)
                                {
                                    sectionsFetched++;
                                }
                            }
                            var tracksElapsed = DateTime.UtcNow - tracksStartTime;

                            logger.Information("✓ Fetched tracks for {Fetched}/{Total} section(s) in {ElapsedMs}ms",
                                sectionsFetched, sectionCodes.Count, tracksElapsed.TotalMilliseconds);

                            // Total time includes both sections and tracks
                            var totalTimeForLanguage = DateTime.UtcNow - languageStartTime;
                            languageTimes[testLanguageCode] = (totalTimeForLanguage, sectionsElapsed, tracksElapsed, sectionCodes.Count, null);
                        }
                        else
                        {
                            var totalTimeForLanguage = DateTime.UtcNow - languageStartTime;
                            languageTimes[testLanguageCode] = (totalTimeForLanguage, sectionsElapsed, null, null, null);
                        }
                    }
                    else
                    {
                        // Publication has no sections: Fetch all tracks directly
                        logger.Information("Fetching tracks for publication {PublicationCode} in language {LanguageCode}...",
                            publicationCode, testLanguageCode);

                        var pubTracksStartTime = DateTime.UtcNow;
                        success = await languageContentService.FetchPublicationTracksAsync(
                            normalizedPublicationCode, normalizedTestLanguageCode);
                        var pubTracksElapsed = DateTime.UtcNow - pubTracksStartTime;

                        var elapsed = DateTime.UtcNow - languageStartTime;
                        languageTimes[testLanguageCode] = (elapsed, null, null, null, pubTracksElapsed);

                        if (success)
                        {
                            logger.Information("✓ Successfully fetched {PublicationCode} for {LanguageCode} in {ElapsedMs}ms",
                                publicationCode, testLanguageCode, elapsed.TotalMilliseconds);
                        }
                        else
                        {
                            logger.Warning("✗ Failed to fetch {PublicationCode} for {LanguageCode} (took {ElapsedMs}ms)",
                                publicationCode, testLanguageCode, elapsed.TotalMilliseconds);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var elapsed = DateTime.UtcNow - languageStartTime;
                    languageTimes[testLanguageCode] = (elapsed, null, null, null, null);
                    logger.Error(ex, "✗ Error fetching {PublicationCode} for {LanguageCode} (took {ElapsedMs}ms)",
                        publicationCode, testLanguageCode, elapsed.TotalMilliseconds);
                }
            }

            publicationStats.Add((publicationCode, languageTimes));
        }

        var totalElapsed = DateTime.UtcNow - totalStartTime;

        // Log summary statistics
        logger.Information("=== TEST MODE: On-Demand Fetching Summary ===");
        logger.Information("Total time: {TotalSeconds:F2}s", totalElapsed.TotalSeconds);
        logger.Information("Publications tested: {Count}", publicationStats.Count);

        // Calculate averages per language
        foreach (var testLanguageCode in testLanguages)
        {
            var times = publicationStats
                .SelectMany(ps => ps.LanguageTimes.Where(lt => lt.Key == testLanguageCode).Select(lt => lt.Value.Total))
                .ToList();

            if (times.Count > 0)
            {
                var avgTime = TimeSpan.FromMilliseconds(times.Average(t => t.TotalMilliseconds));
                var minTime = times.Min();
                var maxTime = times.Max();
                logger.Information("Language {LanguageCode}: {Count} fetched, Avg: {AvgMs:F0}ms, Min: {MinMs:F0}ms, Max: {MaxMs:F0}ms",
                    testLanguageCode, times.Count, avgTime.TotalMilliseconds, minTime.TotalMilliseconds, maxTime.TotalMilliseconds);
            }
        }

        // Calculate separate averages for different operation types
        var sectionsTimes = publicationStats
            .SelectMany(ps => ps.LanguageTimes.Values.Where(v => v.Sections.HasValue).Select(v => v.Sections!.Value))
            .ToList();
        if (sectionsTimes.Count > 0)
        {
            var avgSections = TimeSpan.FromMilliseconds(sectionsTimes.Average(t => t.TotalMilliseconds));
            logger.Information("=== Fetching Sections Statistics ===");
            logger.Information("  Count: {Count}, Avg: {AvgMs:F0}ms, Min: {MinMs:F0}ms, Max: {MaxMs:F0}ms",
                sectionsTimes.Count, avgSections.TotalMilliseconds, sectionsTimes.Min().TotalMilliseconds, sectionsTimes.Max().TotalMilliseconds);
        }

        var sectionTracksTimes = publicationStats
            .SelectMany(ps => ps.LanguageTimes.Values.Where(v => v.SectionTracks.HasValue && v.SectionCount.HasValue)
                .Select(v => (Time: v.SectionTracks!.Value, Count: v.SectionCount!.Value)))
            .ToList();
        if (sectionTracksTimes.Count > 0)
        {
            var avgSectionTracks = TimeSpan.FromMilliseconds(sectionTracksTimes.Average(t => t.Time.TotalMilliseconds));
            var totalSections = sectionTracksTimes.Sum(t => t.Count);
            var avgPerSection = TimeSpan.FromMilliseconds(sectionTracksTimes.Average(t => t.Time.TotalMilliseconds / Math.Max(1, t.Count)));
            logger.Information("=== Fetching Section Tracks Statistics ===");
            logger.Information("  Publications: {Count}, Total Sections: {TotalSections}, Avg per publication: {AvgMs:F0}ms, Avg per section: {AvgPerSectionMs:F0}ms",
                sectionTracksTimes.Count, totalSections, avgSectionTracks.TotalMilliseconds, avgPerSection.TotalMilliseconds);
            logger.Information("  Min: {MinMs:F0}ms, Max: {MaxMs:F0}ms",
                sectionTracksTimes.Min(t => t.Time).TotalMilliseconds, sectionTracksTimes.Max(t => t.Time).TotalMilliseconds);
        }

        var publicationTracksTimes = publicationStats
            .SelectMany(ps => ps.LanguageTimes.Values.Where(v => v.PublicationTracks.HasValue).Select(v => v.PublicationTracks!.Value))
            .ToList();
        if (publicationTracksTimes.Count > 0)
        {
            var avgPubTracks = TimeSpan.FromMilliseconds(publicationTracksTimes.Average(t => t.TotalMilliseconds));
            logger.Information("=== Fetching Publication Tracks Statistics (non-sectioned) ===");
            logger.Information("  Count: {Count}, Avg: {AvgMs:F0}ms, Min: {MinMs:F0}ms, Max: {MaxMs:F0}ms",
                publicationTracksTimes.Count, avgPubTracks.TotalMilliseconds, publicationTracksTimes.Min().TotalMilliseconds, publicationTracksTimes.Max().TotalMilliseconds);
        }

        // Log per-publication statistics with breakdown
        logger.Information("=== Per-Publication Statistics ===");
        foreach (var (pubCode, langTimes) in publicationStats.OrderBy(ps => ps.PublicationCode))
        {
            if (langTimes.Count > 0)
            {
                var timesStr = string.Join(", ", langTimes.Select(lt =>
                {
                    var (total, sections, sectionTracks, sectionCount, pubTracks) = lt.Value;
                    if (sections.HasValue && sectionTracks.HasValue && sectionCount.HasValue)
                    {
                        var perSection = sectionTracks.Value.TotalMilliseconds / Math.Max(1, sectionCount.Value);
                        return $"{lt.Key}: {total.TotalMilliseconds:F0}ms (sections: {sections.Value.TotalMilliseconds:F0}ms, tracks: {sectionTracks.Value.TotalMilliseconds:F0}ms for {sectionCount.Value} sections, ~{perSection:F0}ms/section)";
                    }
                    else if (pubTracks.HasValue)
                    {
                        return $"{lt.Key}: {total.TotalMilliseconds:F0}ms (tracks: {pubTracks.Value.TotalMilliseconds:F0}ms)";
                    }
                    else
                    {
                        return $"{lt.Key}: {total.TotalMilliseconds:F0}ms";
                    }
                }));
                logger.Information("  {PublicationCode}: {Times}", pubCode, timesStr);
            }
        }
    }


    #endregion
}
