#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Seeders;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using BiblePublicationSection = Bible.Alarm.Cataloger.Models.BiblePublications.BiblePublicationSection;
using BiblePublicationTrack = Bible.Alarm.Cataloger.Models.BiblePublications.BiblePublicationTrack;
using Language = Bible.Alarm.Shared.Models.Media.Language;
using MediatorTrack = Bible.Alarm.Cataloger.Models.MediatorTrack;
using MusicTrack = Bible.Alarm.Cataloger.Models.MusicTrack;
using SharedBiblePublicationSection = Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection;
using SharedBiblePublicationTrack = Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationTrack;
using VideoEpisode = Bible.Alarm.Cataloger.Models.VideoEpisode;

namespace Bible.Alarm.Cataloger.Utility;

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
    private readonly SpanishSeeder spanishSeeder;
    private readonly string failedListPath;

    /// <summary>
    /// Exposes the PublicationLanguages data store for access by catalogers after discovery phase.
    /// </summary>
    public IReadOnlyDictionary<string, Dictionary<string, LanguageInfo>> PublicationLanguages => dataStore.PublicationLanguages;

    public DbSeeder(ILogger logger, IServiceScopeFactory scopeFactory, DownloadUtility downloadUtility, bool isTestRun = false, string? failedListPath = null)
    {
        this.logger = logger;
        this.scopeFactory = scopeFactory;
        this.downloadUtility = downloadUtility;
        this.dataStore = new InMemoryDataStore();
        this.isTestRun = isTestRun;
        this.failedListPath = failedListPath ?? Path.Combine(Path.GetTempPath(), AppConstants.FilePaths.CatalogerTempFailedListFallbackFileName);
        
        // Initialize helper classes
        this.languageSeeder = new LanguageSeeder(logger, downloadUtility);
        this.categorySeeder = new CategorySeeder(logger);
        this.publicationLanguageSeeder = new PublicationLanguageSeeder(logger, dataStore, languageSeeder);
        this.sectionLanguageSeeder = new SectionLanguageSeeder(logger, dataStore, languageSeeder);
        this.testModeSeeder = new TestModeSeeder(logger, scopeFactory, dataStore);
        this.englishSeeder = new EnglishSeeder(logger, scopeFactory, dataStore);
        this.melodyMusicSeeder = new MelodyMusicSeeder(logger, scopeFactory, dataStore);
        this.spanishSeeder = new SpanishSeeder(logger, scopeFactory);
    }

    public async Task Seed(IReadOnlySet<string>? publicationFilter = null)
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
                throw new InvalidOperationException("Failed to apply migrations", ex);
            }
        }

        // Seed default Categories first
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

        // Pre-seed SectionLanguages for IssueSectioned (magazine) publications before English seeding.
        // EnglishContentSeeder queries SectionLanguages to determine which section codes to fetch for magazines.
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            await sectionLanguageSeeder.SeedMagazineSectionLanguages(db);
        }

        // Seed English using shared FetchAndSave* methods (same as used for other languages in test mode)
        // This happens after discovery tables are seeded, using the same methods that are used for on-demand fetching
        await englishSeeder.SeedEnglish(publicationFilter, failedListPath);

        // Sync PublicationLanguages for E so all E BiblePublications (incl. VOD*, Series*) appear in publication list
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            await publicationLanguageSeeder.SyncPublicationLanguagesForEnglishAsync(db);
            await db.SaveChangesAsync();
        }

        // Now seed SectionLanguages after English publications exist (needed for section lookup)
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            await sectionLanguageSeeder.SeedSectionLanguages(db);
        }

        // Add Spanish (S) to discovery tables and seed Spanish using same ad-hoc fetch as schedule page
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            await publicationLanguageSeeder.SyncPublicationLanguagesForSpanishAsync(db);
            await sectionLanguageSeeder.SyncSectionLanguagesForSpanishAsync(db);
            await db.SaveChangesAsync();
        }
        await spanishSeeder.SeedSpanishAsync();
        
        // In test mode, also seed MY and A after English
        if (isTestRun)
        {
            await testModeSeeder.SeedTestLanguages();
        }
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

    public Task SaveMediatorPublication(
        string languageCode,
        string publicationCode,
        string publicationName,
        Dictionary<string, List<MediatorTrack>> tracksBySection,
        Dictionary<string, string> sectionNames)
    {
        var key = (languageCode.ToUpperInvariant(), publicationCode.ToLowerInvariant());
        dataStore.MediatorPublications[key] = (
            publicationName,
            new Dictionary<string, List<MediatorTrack>>(tracksBySection, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(sectionNames, StringComparer.OrdinalIgnoreCase));
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
        dataStore.MelodyMusic[publicationCode.ToLowerInvariant()] = (
            new Dictionary<string, List<MusicTrack>>(discTracksMap, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(discNamesMap, StringComparer.OrdinalIgnoreCase));
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
        dataStore.LanguageDiscovery[key] = new Dictionary<string, string>(languageCodeToNameMapping, StringComparer.OrdinalIgnoreCase);
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
                dataStore.PublicationLanguages[normalizedPublicationCode] =
                    new Dictionary<string, LanguageInfo>(languagesToSave, StringComparer.OrdinalIgnoreCase);
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
                dataStore.SectionLanguages[key] = new Dictionary<string, LanguageInfo>(languagesToSave, StringComparer.OrdinalIgnoreCase);
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
