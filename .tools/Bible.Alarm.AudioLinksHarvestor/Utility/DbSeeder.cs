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
using Bible.Alarm.AudioLinksHarvestor.Utility.Helpers;
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
using SharedBiblePublicationSection = Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection;
using SharedBiblePublicationTrack = Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationTrack;
using SharedUrlParam = Bible.Alarm.Shared.Models.Media.BiblePublications.UrlParam;
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
    private readonly bool isTestRun;
    private readonly LanguageSeeder languageSeeder;
    private readonly CategorySeeder categorySeeder;
    private readonly PublicationLanguageSeeder publicationLanguageSeeder;
    private readonly SectionLanguageSeeder sectionLanguageSeeder;
    private readonly TestModeSeeder testModeSeeder;

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
        
        // Initialize helper classes
        this.languageSeeder = new LanguageSeeder(logger, downloadUtility);
        this.categorySeeder = new CategorySeeder(logger);
        this.publicationLanguageSeeder = new PublicationLanguageSeeder(logger, dataStore, languageSeeder);
        this.sectionLanguageSeeder = new SectionLanguageSeeder(logger, dataStore, languageSeeder);
        this.testModeSeeder = new TestModeSeeder(logger, scopeFactory, dataStore);
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
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            await categorySeeder.SeedDefaultCategoriesAndApiUrls(db);
        }
        
        // Seed ALL languages from jw.org /en/languages API to Languages table
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            await languageSeeder.SeedAllLanguagesFromJwOrg(db);
        }
        
        // Seed discovered languages for on-demand fetching (discovery tables)
        // Note: Only seed PublicationLanguages here - SectionLanguages needs English publications to exist first
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            await publicationLanguageSeeder.SeedPublicationLanguages(db);
        }
        
        // Seed MelodyMusic publications (instrumental music without language)
        // This must happen before SeedEnglish() because SeedEnglish() skips publications with LanguageId == null
        await SeedMelodyMusic();
        
        // Seed English using shared FetchAndSave* methods (same as used for other languages in test mode)
        // This happens after discovery tables are seeded, using the same methods that are used for on-demand fetching
        await SeedEnglish();
        
        // Now seed SectionLanguages after English publications exist (needed for section lookup)
        using (var scope = scopeFactory.CreateScope())
        {
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            await sectionLanguageSeeder.SeedSectionLanguages(db);
        }
        
        // In test mode, also seed MY and A after English
        if (isTestRun)
        {
            await testModeSeeder.SeedTestLanguages();
        }
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

    /// <summary>
    /// Seeds English (E) for all discovered publications using the shared FetchAndSave* methods.
    /// This is the same approach used for other languages in test mode.
    /// </summary>
    private async Task SeedEnglish()
    {
        using var scope = scopeFactory.CreateScope();
        var languageContentService = scope.ServiceProvider.GetRequiredService<Bible.Alarm.Shared.Services.Media.Interfaces.ILanguageContentService>();

        logger.Information("=== Seeding English (E) for all discovered publications ===");

        using var dbScope = scopeFactory.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<MediaDbContext>();

        // Data-driven approach: Get all publications that need English seeding
        // This includes:
        // 1. Publications from discovery phase (dataStore.PublicationLanguages) that haven't been harvested yet
        // 2. Publications that have been harvested but don't have English yet (excluding those with LanguageId == null)
        
        // Step 1: Get all distinct publication codes from discovery phase
        var discoveredPublicationCodes = dataStore.PublicationLanguages.Keys.ToList();

        // Step 2: Get all distinct publication codes from BiblePublications (excluding those with LanguageId == null)
        // Publications with LanguageId == null don't have English content, so skip them
        var harvestedPublicationCodes = await db.BiblePublications
            .AsNoTracking()
            .Where(bp => bp.LanguageId != null) // Exclude publications without language (they don't have English content)
            .Select(bp => bp.PublicationCode)
            .Distinct()
            .ToListAsync();

        // Step 3: Combine both lists and get unique publication codes
        var allPublicationCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in discoveredPublicationCodes)
        {
            allPublicationCodes.Add(code);
        }
        foreach (var code in harvestedPublicationCodes)
        {
            allPublicationCodes.Add(code);
        }

        // Step 4: Filter out publications that already have English or have LanguageId == null
        var publicationsNeedingEnglish = new List<string>();
        foreach (var publicationCode in allPublicationCodes)
        {
            // Normalize publication code for database queries
            var normalizedCode = publicationCode.ToLowerInvariant();
            var isDrama = PublicationTypeHelper.IsDrama(normalizedCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = normalizedCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = normalizedCode;
            }

            // Check if publication has LanguageId == null (skip these - they don't have English content)
            var hasNullLanguage = await db.BiblePublications
                .AsNoTracking()
                .AnyAsync(bp => bp.PublicationCode == publicationCodeForDb && bp.LanguageId == null);

            if (hasNullLanguage)
            {
                logger.Debug("Skipping publication {PublicationCode} - has LanguageId == null (no English content)", publicationCode);
                continue;
            }

            // Check if English already exists for this publication
            var hasEnglish = await db.BiblePublications
                .AsNoTracking()
                .Include(bp => bp.Language)
                .AnyAsync(bp => bp.PublicationCode == publicationCodeForDb &&
                               bp.Language != null &&
                               bp.Language.LanguageCode == "E");

            if (!hasEnglish)
            {
                publicationsNeedingEnglish.Add(publicationCode);
            }
        }

        if (publicationsNeedingEnglish.Count == 0)
        {
            logger.Information("All publications already have English seeded or have LanguageId == null, skipping");
            return;
        }

        logger.Information("Found {Count} publication(s) that need English seeding", publicationsNeedingEnglish.Count);

        foreach (var publicationCode in publicationsNeedingEnglish.OrderBy(pc => pc))
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

    /// <summary>
    /// Seeds MelodyMusic publications (instrumental music without language) to the database.
    /// Each disc becomes a section, and tracks within each disc become tracks under that section.
    /// </summary>
    private async Task SeedMelodyMusic()
    {
        if (dataStore.MelodyMusic.Count == 0)
        {
            logger.Information("No MelodyMusic publications to seed");
            return;
        }

        logger.Information("=== Seeding MelodyMusic publications ===");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        // Get Music category
        var musicCategory = await db.Categories
            .FirstOrDefaultAsync(c => c.CategoryName == "Music");

        if (musicCategory == null)
        {
            logger.Error("Music category not found in database");
            return;
        }

        // Get BaseUrl for creating UrlParams
        var baseUrl = await db.BaseUrls
            .FirstOrDefaultAsync(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS");

        if (baseUrl == null)
        {
            logger.Error("BaseUrl not found for GETPUBMEDIALINKS");
            return;
        }

        foreach (var kvp in dataStore.MelodyMusic)
        {
            var publicationCode = kvp.Key;
            var (discTracksMap, discNamesMap) = kvp.Value;

            if (discTracksMap.Count == 0)
            {
                logger.Warning("No discs found for MelodyMusic publication {PublicationCode}", publicationCode);
                continue;
            }

            // Check if publication already exists
            var existingPublication = await db.BiblePublications
                .Include(bp => bp.Sections)
                .FirstOrDefaultAsync(bp => bp.PublicationCode == publicationCode && bp.LanguageId == null);

            if (existingPublication != null)
            {
                logger.Information("MelodyMusic publication {PublicationCode} already exists, skipping", publicationCode);
                continue;
            }

            // Get publication name - try to get it from the first disc name, or use publication code
            var publicationName = publicationCode.ToUpperInvariant();
            if (discNamesMap.Count > 0)
            {
                var firstDiscName = discNamesMap.Values.First();
                if (!string.IsNullOrEmpty(firstDiscName))
                {
                    // For iam, the disc name might be something like "Kingdom Melodies 1"
                    // Try to extract a better publication name
                    if (firstDiscName.Contains("Kingdom Melodies", StringComparison.OrdinalIgnoreCase))
                    {
                        publicationName = "Kingdom Melodies";
                    }
                    else
                    {
                        publicationName = firstDiscName;
                    }
                }
            }

            // Create BiblePublication using shared model
            var biblePublication = new BiblePublication
            {
                PublicationCode = publicationCode.ToLowerInvariant(),
                Name = publicationName,
                LanguageId = null, // MelodyMusic has no language
                CategoryId = musicCategory.Id,
                Category = musicCategory,
                IsVideo = false,
                Sections = new List<SharedBiblePublicationSection>(),
                Tracks = new List<SharedBiblePublicationTrack>()
            };

            // Create sections from discs
            foreach (var discEntry in discTracksMap.OrderBy(d => d.Key))
            {
                var discCode = discEntry.Key;
                var discTracks = discEntry.Value;

                if (discTracks.Count == 0)
                {
                    continue;
                }

                // Get section name from discNamesMap, or use disc code
                var sectionName = discNamesMap.TryGetValue(discCode, out var name) && !string.IsNullOrEmpty(name)
                    ? name
                    : discCode;

                // Create section using shared model
                var section = new SharedBiblePublicationSection
                {
                    Name = sectionName,
                    SectionCode = discCode.ToLowerInvariant(),
                    BiblePublication = biblePublication,
                    BiblePublicationId = 0, // Will be set after publication is saved
                    Tracks = new List<SharedBiblePublicationTrack>(),
                    UrlParams = new List<SharedUrlParam>()
                };

                // Create tracks for this section
                foreach (var musicTrack in discTracks.OrderBy(t => t.Number))
                {
                    // Create UrlParams for the track (similar to ParseIamTracks)
                    var trackUrlParams = new List<SharedUrlParam>
                    {
                        new SharedUrlParam
                        {
                            Key = "pub",
                            Value = discCode.ToLowerInvariant(), // Use disc code (e.g., "iam-1") as pub parameter
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        },
                        new SharedUrlParam
                        {
                            Key = "fileformat",
                            Value = "mp3",
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        },
                        new SharedUrlParam
                        {
                            Key = "track",
                            Value = (musicTrack.OriginalTrackNumber ?? musicTrack.Number).ToString(),
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        }
                    };

                    // For Kingdom Melodies (iam), prefix track title with "Melody Number(s) "
                    var trackTitle = musicTrack.Title;
                    if (publicationCode.Equals("iam", StringComparison.OrdinalIgnoreCase))
                    {
                        trackTitle = $"Melody Number(s) {trackTitle}";
                    }

                    var track = new SharedBiblePublicationTrack
                    {
                        Number = musicTrack.Number,
                        Title = trackTitle,
                        Section = section,
                        BiblePublicationSectionId = 0, // Will be set after section is saved
                        Publication = biblePublication,
                        BiblePublicationId = 0, // Will be set after publication is saved
                        UrlParams = trackUrlParams
                    };

                    section.Tracks.Add(track);
                }

                biblePublication.Sections.Add(section);
            }

            if (biblePublication.Sections.Count == 0)
            {
                logger.Warning("No sections created for MelodyMusic publication {PublicationCode}", publicationCode);
                continue;
            }

            // Save to database
            db.BiblePublications.Add(biblePublication);
            await db.SaveChangesAsync();

            logger.Information("✓ Successfully seeded MelodyMusic publication {PublicationCode} with {SectionCount} sections and {TrackCount} total tracks",
                publicationCode, biblePublication.Sections.Count, biblePublication.Sections.Sum(s => s.Tracks.Count));
        }

        logger.Information("=== MelodyMusic seeding completed ===");
    }

    // Methods moved to PublicationLanguageSeeder helper class

    public async Task TestOnDemandFetching()
    {
        await testModeSeeder.TestOnDemandFetching();
    }
}
