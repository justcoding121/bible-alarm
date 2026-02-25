#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.AudioLinksHarvestor.Models;
using Bible.Alarm.AudioLinksHarvestor.Utility;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Seeders;

/// <summary>
/// Helper class for seeding publication languages.
/// </summary>
internal sealed class PublicationLanguageSeeder
{
    private readonly ILogger logger;
    private readonly InMemoryDataStore dataStore;
    private readonly LanguageSeeder languageSeeder;

    public PublicationLanguageSeeder(ILogger logger, InMemoryDataStore dataStore, LanguageSeeder languageSeeder)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        this.languageSeeder = languageSeeder ?? throw new ArgumentNullException(nameof(languageSeeder));
    }

    public async Task SeedPublicationLanguages(MediaDbContext db)
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
        foreach (var ((publicationCode, sectionCode), languages) in dataStore.SectionLanguages)
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
            await languageSeeder.GetOrCreateLanguageByCode(db, languageCode);
        }

        // First, always seed E for all English publications
        await SeedEnglishForAllPublications(db);
        // Save E entries first to avoid duplicates
        await db.SaveChangesAsync();

        // Seed publications without language (LanguageId == null) - data-driven, not hard-coded
        // These are publications like "iam" (instrumental music) that don't have a language
        await SeedPublicationsWithoutLanguage(db);
        // Save entries without language
        await db.SaveChangesAsync();

        // Seed publication languages
        if (dataStore.PublicationLanguages.Count == 0)
        {
            logger.Information("Seeded English (E) for all publications and publications without language");
            return;
        }

        logger.Information("Seeding discovered languages for {Count} publication(s)", dataStore.PublicationLanguages.Count);

        var addedInBatch = new HashSet<(string PublicationCode, int? LanguageId)>();
        foreach (var (publicationCode, languages) in dataStore.PublicationLanguages)
        {
            foreach (var (languageCode, languageInfo) in languages)
            {
                await SeedLanguageForPublication(db, publicationCode, languageCode, addedInBatch);
            }
        }

        await db.SaveChangesAsync();
        logger.Information("Seeded publication languages");
    }

    private async Task SeedLanguageForPublication(MediaDbContext db, string publicationCode, string languageCode, HashSet<(string PublicationCode, int? LanguageId)> addedInBatch)
    {
        var normalizedPublicationCode = publicationCode.ToLowerInvariant();
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var publicationCodeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(normalizedPublicationCode) ?? normalizedPublicationCode;

        // Get or create language
        var language = await languageSeeder.GetOrCreateLanguageByCode(db, normalizedLanguageCode);
        var key = (publicationCodeForDb, (int?)language.Id);

        if (addedInBatch.Contains(key))
        {
            return;
        }

        // Determine harvest type and category based on publication code
        var harvestType = PublicationTypeHelper.GetHarvestType(normalizedPublicationCode);
        var categoryCode = JwSourceHelper.GetCategoryCode(normalizedPublicationCode);

        if (string.IsNullOrEmpty(categoryCode))
        {
            logger.Warning("Category not found for publication code {PublicationCode}, defaulting to 'Bible'", publicationCode);
            categoryCode = "Bible";
        }

        var category = await db.Categories.FirstOrDefaultAsync(c => c.CategoryCode == categoryCode);
        if (category == null)
        {
            logger.Warning("Category '{CategoryCode}' not found in database for publication {PublicationCode}", categoryCode, publicationCode);
            return;
        }

        var exists = await db.PublicationLanguages
            .AnyAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == language.Id);

        var isMusic = !JwSourceHelper.IsMusicExcludedPublicationCodes.Contains(publicationCode) &&
            (string.Equals(categoryCode, "Music", StringComparison.OrdinalIgnoreCase) ||
             JwSourceHelper.IsMusicPublicationCode(publicationCode));

        if (!exists)
        {
            var publicationLanguage = new PublicationLanguage
            {
                PublicationCode = publicationCodeForDb,
                Language = language,
                HarvestType = harvestType,
                Category = category,
                CategoryId = category.Id,
                IsMusic = isMusic
            };
            db.PublicationLanguages.Add(publicationLanguage);
            addedInBatch.Add(key);
        }
        else
        {
            var existing = await db.PublicationLanguages
                .FirstOrDefaultAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == language.Id);

            if (existing != null)
            {
                existing.HarvestType = harvestType;
                existing.Category = category;
                existing.CategoryId = category.Id;
                existing.IsMusic = isMusic;
            }
        }
    }

    /// <summary>
    /// Syncs PublicationLanguages so that English (E) has a row for every BiblePublication with Language E.
    /// Call after English seeding so that VOD*, Series*, and other E-only publications appear in the app.
    /// </summary>
    public async Task SyncPublicationLanguagesForEnglishAsync(MediaDbContext db)
    {
        await SeedEnglishForAllPublications(db);
    }

    /// <summary>
    /// Adds Spanish (S) to PublicationLanguages for every publication that has English (E).
    /// Enables ad-hoc fetch and Spanish seeding to run for all publications we seeded in E.
    /// Call after SyncPublicationLanguagesForEnglishAsync.
    /// </summary>
    public async Task SyncPublicationLanguagesForSpanishAsync(MediaDbContext db)
    {
        const string SpanishCode = "S";
        var spanishLanguage = await languageSeeder.GetOrCreateLanguageByCode(db, SpanishCode);

        var englishPublicationLanguages = await db.PublicationLanguages
            .Include(pl => pl.Language)
            .Include(pl => pl.Category)
            .Where(pl => pl.Language != null && pl.Language.LanguageCode == "E")
            .ToListAsync();

        var added = 0;
        foreach (var plE in englishPublicationLanguages)
        {
            var exists = await db.PublicationLanguages
                .AnyAsync(pl => pl.PublicationCode == plE.PublicationCode && pl.LanguageId == spanishLanguage.Id);
            if (exists)
            {
                continue;
            }

            db.PublicationLanguages.Add(new PublicationLanguage
            {
                PublicationCode = plE.PublicationCode,
                Language = spanishLanguage,
                LanguageId = spanishLanguage.Id,
                HarvestType = plE.HarvestType,
                Category = plE.Category,
                CategoryId = plE.CategoryId,
                IsMusic = plE.IsMusic
            });
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync();
            logger.Information("SyncPublicationLanguagesForSpanish: Added Spanish (S) for {Count} publication(s)", added);
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

        var englishLanguage = await languageSeeder.GetOrCreateLanguageByCode(db, "E");

        foreach (var publicationCode in englishPublications)
        {
            var normalizedPublicationCode = publicationCode.ToLowerInvariant();

            // Preserve actual publication code from BiblePublications (VODMoviesBibleTimes, Dramas, DramasGoodNews, etc.)
            var publicationCodeForDb = publicationCode;

            var harvestType = PublicationTypeHelper.GetHarvestType(normalizedPublicationCode);
            var categoryCode = JwSourceHelper.GetCategoryCode(normalizedPublicationCode);

            if (string.IsNullOrEmpty(categoryCode))
            {
                logger.Warning("Category not found for publication code {PublicationCode}, defaulting to 'Bible'", publicationCode);
                categoryCode = "Bible";
            }

            var category = await db.Categories.FirstOrDefaultAsync(c => c.CategoryCode == categoryCode);
            if (category == null)
            {
                logger.Warning("Category '{CategoryCode}' not found in database for publication {PublicationCode}", categoryCode, publicationCode);
                continue;
            }

            var isMusic = !JwSourceHelper.IsMusicExcludedPublicationCodes.Contains(publicationCode) &&
                (string.Equals(categoryCode, "Music", StringComparison.OrdinalIgnoreCase) ||
                 JwSourceHelper.IsMusicPublicationCode(publicationCode));

            var exists = await db.PublicationLanguages
                .AnyAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == englishLanguage.Id);

            if (!exists)
            {
                var publicationLanguage = new PublicationLanguage
                {
                    PublicationCode = publicationCodeForDb,
                    Language = englishLanguage,
                    HarvestType = harvestType,
                    Category = category,
                    CategoryId = category.Id,
                    IsMusic = isMusic
                };
                db.PublicationLanguages.Add(publicationLanguage);
            }
            else
            {
                var existing = await db.PublicationLanguages
                    .FirstOrDefaultAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == englishLanguage.Id);

                if (existing != null)
                {
                    existing.HarvestType = harvestType;
                    existing.Category = category;
                    existing.CategoryId = category.Id;
                    existing.IsMusic = isMusic;
                }
            }
        }

        await EnsureDramasCategoryEntriesForDramaPublicationsAsync(db, englishPublications, englishLanguage);
    }

    /// <summary>
    /// For publications whose primary category is Dramas (DramaCategoryCodes or VideoPublicationCodes),
    /// ensure a PublicationLanguage row with Category = Dramas so they appear in the Dramas list.
    /// Series/Children pubs (e.g. SeriesBJFLessons, SeriesDigForTreasures, VODLFFVideosAD) are not added here.
    /// </summary>
    private async Task EnsureDramasCategoryEntriesForDramaPublicationsAsync(
        MediaDbContext db,
        List<string> englishPublicationCodes,
        Bible.Alarm.Shared.Models.Media.Language englishLanguage)
    {
        var dramasCategory = await db.Categories.FirstOrDefaultAsync(c => c.CategoryCode == "Dramas");
        if (dramasCategory == null)
        {
            return;
        }

        var withDramasCategory = await db.PublicationLanguages
            .Where(pl => pl.CategoryId == dramasCategory.Id)
            .ToListAsync();
        var toRemove = withDramasCategory
            .Where(pl => !JwSourceHelper.IsInDramasCategory(pl.PublicationCode))
            .ToList();
        if (toRemove.Count > 0)
        {
            db.PublicationLanguages.RemoveRange(toRemove);
            await db.SaveChangesAsync();
            logger.Information("Removed {Count} PublicationLanguage row(s) with Dramas category for non-Dramas publications", toRemove.Count);
        }

        foreach (var publicationCode in englishPublicationCodes)
        {
            if (!JwSourceHelper.IsInDramasCategory(publicationCode))
            {
                continue;
            }

            var exists = await db.PublicationLanguages
                .AnyAsync(pl => pl.PublicationCode == publicationCode
                    && pl.LanguageId == englishLanguage.Id
                    && pl.CategoryId == dramasCategory.Id);

            if (exists)
            {
                continue;
            }

            var harvestType = PublicationTypeHelper.GetHarvestType(publicationCode);
            db.PublicationLanguages.Add(new PublicationLanguage
            {
                PublicationCode = publicationCode,
                Language = englishLanguage,
                HarvestType = harvestType,
                Category = dramasCategory,
                CategoryId = dramasCategory.Id,
                IsMusic = false
            });
        }
    }

    /// <summary>
    /// Seeds PublicationLanguage entries for publications without language (LanguageId == null).
    /// This is data-driven - finds all publications in BiblePublications with LanguageId == null
    /// and creates corresponding PublicationLanguage entries.
    /// </summary>
    private async Task SeedPublicationsWithoutLanguage(MediaDbContext db)
    {
        // Get all publications without language (LanguageId == null) - data-driven, not hard-coded
        var publicationsWithoutLanguage = await db.BiblePublications
            .AsNoTracking()
            .Include(bp => bp.BiblePublicationCategories)
            .ThenInclude(bpc => bpc.Category)
            .Where(bp => bp.LanguageId == null)
            .ToListAsync();

        if (publicationsWithoutLanguage.Count == 0)
        {
            logger.Debug("No publications without language found to seed");
            return;
        }

        logger.Information("Seeding PublicationLanguage entries for {Count} publication(s) without language", publicationsWithoutLanguage.Count);

        foreach (var publication in publicationsWithoutLanguage)
        {
            if (publication.PrimaryCategory == null)
            {
                logger.Warning("Category not found for publication {PublicationCode} without language, skipping", publication.PublicationCode);
                continue;
            }

            var normalizedPublicationCode = publication.PublicationCode.ToLowerInvariant();
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

            // Determine harvest type based on publication code
            var harvestType = PublicationTypeHelper.GetHarvestType(normalizedPublicationCode);

            // Check if already exists (with LanguageId == null)
            var exists = await db.PublicationLanguages
                .AnyAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == null);

            if (!exists)
            {
                var category = publication.PrimaryCategory;
                var publicationLanguage = new PublicationLanguage
                {
                    PublicationCode = publicationCodeForDb,
                    LanguageId = null,
                    Language = null,
                    HarvestType = harvestType,
                    Category = category,
                    CategoryId = category.Id,
                    IsMusic = publication.IsMusic
                };
                db.PublicationLanguages.Add(publicationLanguage);
                logger.Debug("Added PublicationLanguage entry for {PublicationCode} without language (Category: {CategoryCode})",
                    publicationCodeForDb, category.CategoryCode);
            }
            else
            {
                var existing = await db.PublicationLanguages
                    .FirstOrDefaultAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == null);

                if (existing != null)
                {
                    existing.HarvestType = harvestType;
                    existing.Category = publication.PrimaryCategory;
                    existing.CategoryId = publication.PrimaryCategoryId;
                    existing.IsMusic = publication.IsMusic;
                }
            }
        }
    }
}
