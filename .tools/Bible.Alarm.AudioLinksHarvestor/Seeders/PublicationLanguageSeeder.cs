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
            await languageSeeder.GetOrCreateLanguageByCode(db, languageCode);
        }

        // First, always seed E for all English publications
        await SeedEnglishForAllPublications(db);
        await db.SaveChangesAsync(); // Save E entries first to avoid duplicates

        // Seed publications without language (LanguageId == null) - data-driven, not hard-coded
        // These are publications like "iam" (instrumental music) that don't have a language
        await SeedPublicationsWithoutLanguage(db);
        await db.SaveChangesAsync(); // Save entries without language

        // Seed publication languages
        if (dataStore.PublicationLanguages.Count == 0)
        {
            logger.Information("Seeded English (E) for all publications and publications without language");
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
        var language = await languageSeeder.GetOrCreateLanguageByCode(db, normalizedLanguageCode);
        
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
            var publicationLanguage = new PublicationLanguage
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

        var englishLanguage = await languageSeeder.GetOrCreateLanguageByCode(db, "E");

        foreach (var publicationCode in englishPublications)
        {
            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            
            // For dramas, use case-sensitive publication codes: "Dramas" or "DramaticBibleReadings"
            // This matches the logic in SeedLanguageForPublication to prevent duplicates
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
            
            // Check if already exists (use case-sensitive code for dramas)
            var exists = await db.PublicationLanguages
                .AnyAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == englishLanguage.Id);

            if (!exists)
            {
                var publicationLanguage = new PublicationLanguage
                {
                    PublicationCode = publicationCodeForDb, // Use case-sensitive code for dramas
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
                    .FirstOrDefaultAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == englishLanguage.Id);
                
                if (existing != null)
                {
                    existing.HarvestType = harvestType;
                    existing.Category = category;
                    existing.CategoryId = category.Id;
                }
            }
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
            .Include(bp => bp.Category)
            .Where(bp => bp.LanguageId == null)
            .Select(bp => new { bp.PublicationCode, bp.Category })
            .Distinct()
            .ToListAsync();

        if (publicationsWithoutLanguage.Count == 0)
        {
            logger.Debug("No publications without language found to seed");
            return;
        }

        logger.Information("Seeding PublicationLanguage entries for {Count} publication(s) without language", publicationsWithoutLanguage.Count);

        foreach (var pub in publicationsWithoutLanguage)
        {
            if (pub.Category == null)
            {
                logger.Warning("Category not found for publication {PublicationCode} without language, skipping", pub.PublicationCode);
                continue;
            }

            // Normalize publication code for database (case-sensitive for dramas)
            var normalizedPublicationCode = pub.PublicationCode.ToLowerInvariant();
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
                // Query Category from database to ensure it's tracked
                var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == pub.Category.Id);
                if (category == null)
                {
                    logger.Warning("Category with Id {CategoryId} not found in database for publication {PublicationCode}, skipping", pub.Category.Id, pub.PublicationCode);
                    continue;
                }

                var publicationLanguage = new PublicationLanguage
                {
                    PublicationCode = publicationCodeForDb,
                    LanguageId = null, // No language FK for publications without language
                    Language = null,
                    HarvestType = harvestType,
                    Category = category,
                    CategoryId = category.Id
                };
                db.PublicationLanguages.Add(publicationLanguage);
                logger.Debug("Added PublicationLanguage entry for {PublicationCode} without language (Category: {CategoryName})",
                    publicationCodeForDb, pub.Category.CategoryName);
            }
            else
            {
                // Update existing entry with harvest type and category if missing
                var existing = await db.PublicationLanguages
                    .FirstOrDefaultAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == null);
                
                if (existing != null)
                {
                    existing.HarvestType = harvestType;
                    existing.Category = pub.Category;
                    existing.CategoryId = pub.Category.Id;
                }
            }
        }
    }
}
