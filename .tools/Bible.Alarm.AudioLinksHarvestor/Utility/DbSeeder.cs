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
using SharedBiblePublicationSection = Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection;
using SharedBiblePublicationTrack = Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationTrack;
using SharedUrlParam = Bible.Alarm.Shared.Models.Media.BiblePublications.UrlParam;
using DramaTrack = Bible.Alarm.AudioLinksHarvestor.Models.DramaTrack;
using MusicTrack = Bible.Alarm.AudioLinksHarvestor.Models.MusicTrack;
using Publication = Bible.Alarm.AudioLinksHarvestor.Models.Publication;
using VideoEpisode = Bible.Alarm.AudioLinksHarvestor.Models.VideoEpisode;
using Bible.Alarm.AudioLinksHarvestor.Seeders;

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
    private readonly EnglishSeeder englishSeeder;
    private readonly MelodyMusicSeeder melodyMusicSeeder;

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
        this.englishSeeder = new EnglishSeeder(logger, scopeFactory, dataStore);
        this.melodyMusicSeeder = new MelodyMusicSeeder(logger, scopeFactory, dataStore);
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
        await melodyMusicSeeder.SeedMelodyMusic();
        
        // Seed English using shared FetchAndSave* methods (same as used for other languages in test mode)
        // This happens after discovery tables are seeded, using the same methods that are used for on-demand fetching
        await englishSeeder.SeedEnglish();
        
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


    private async Task<List<ApiUrl>> GetAllApiUrls(MediaDbContext db)
    {
        return await db.ApiUrls
            .Where(x => x.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
            .ToListAsync();
    }

    private async Task<Category> GetCategory(MediaDbContext db, string categoryCode)
    {
        return await db.Categories.FirstAsync(x => x.CategoryCode == categoryCode);
    }

    private async Task<BiblePublication> CreateBiblePublication(
        MediaDbContext db,
        Publication publication,
        Language? newLanguage,
        Category category,
        string? languageCode,
        bool isVideo = false)
    {
        var normalizedCode = publication.Code.ToLowerInvariant();
        var biblePublication = new BiblePublication
        {
            Name = publication.Name,
            PublicationCode = normalizedCode,
            Language = newLanguage,
            BiblePublicationCategories = new List<BiblePublicationCategory> { new BiblePublicationCategory { CategoryId = category.Id, Category = category } },
            IsVideo = isVideo
        };
        return biblePublication;
    }



    public Task SaveBiblePublicationSections(
        string languageCode,
        string publicationCode,
        string publicationName,
        Dictionary<int, BiblePublicationSection> sections,
        Dictionary<int, Dictionary<int, BiblePublicationTrack>> sectionCodeTrackMap)
    {
        var key = (languageCode.ToUpperInvariant(), publicationCode.ToLowerInvariant());
        dataStore.BiblePublications[key] = (publicationName, sections, sectionCodeTrackMap);
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

    // Methods moved to PublicationLanguageSeeder helper class

    public async Task TestOnDemandFetching()
    {
        await testModeSeeder.TestOnDemandFetching();
    }
}
