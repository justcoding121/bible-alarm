#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Cataloger.Seeders;

/// <summary>
/// Helper class for seeding section languages.
/// </summary>
internal sealed class SectionLanguageSeeder
{
    private readonly ILogger logger;
    private readonly InMemoryDataStore dataStore;
    private readonly LanguageSeeder languageSeeder;

    public SectionLanguageSeeder(ILogger logger, InMemoryDataStore dataStore, LanguageSeeder languageSeeder)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        this.languageSeeder = languageSeeder ?? throw new ArgumentNullException(nameof(languageSeeder));
    }

    /// <summary>
    /// Pre-seeds SectionLanguage entries for IssueSectioned publications (magazines) before English seeding.
    /// EnglishContentSeeder needs these entries to know which section codes to fetch for magazine publications.
    /// Other publication types (Bible, Mediator) derive their section codes from different sources.
    /// </summary>
    public async Task SeedMagazineSectionLanguages(MediaDbContext db)
    {
        var magazineEntries = dataStore.SectionLanguages
            .Where(kvp => MagazineHelper.IsMagazinePublicationCode(kvp.Key.PublicationCode))
            .ToList();

        if (magazineEntries.Count == 0)
        {
            return;
        }

        logger.Information("Pre-seeding discovered languages for {Count} magazine section(s)", magazineEntries.Count);

        foreach (var ((publicationCode, sectionCode), languages) in magazineEntries)
        {
            var normalizedSectionCode = sectionCode.ToLowerInvariant();
            foreach (var (languageCode, _) in languages)
            {
                await SeedLanguageForSection(db, publicationCode, normalizedSectionCode, languageCode);
            }
        }

        await db.SaveChangesAsync();
        logger.Information("Pre-seeded magazine section languages");
    }

    public async Task SeedSectionLanguages(MediaDbContext db)
    {
        if (dataStore.SectionLanguages.IsEmpty)
        {
            return;
        }

        logger.Debug("Seeding discovered languages for {Count} section(s)", dataStore.SectionLanguages.Count);

        foreach (var ((publicationCode, sectionCode), languages) in dataStore.SectionLanguages)
        {
            // Normalize to lowercase for lookup, but use case-sensitive code for dramas in database
            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            var normalizedSectionCode = sectionCode.ToLowerInvariant();

            var publicationCodeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(normalizedPublicationCode) ?? normalizedPublicationCode;

            // Verify English publication exists (it should be seeded by now, but skip silently if not)
            // Note: We still seed section languages even if English publication doesn't exist,
            // as the discovery phase already found which languages are available for each section
            var englishPublication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.Sections)
                .FirstOrDefaultAsync(bp => bp.PublicationCode == publicationCodeForDb && bp.Language != null && bp.Language.LanguageCode == AppConstants.Media.DefaultLanguageCode);

            // Try to find section in English publication for reference (but don't require it)
            // Note: englishPublication.Sections returns Shared.Models.Media.BiblePublications.BiblePublicationSection, not the cataloger model
            BiblePublicationSection? section = null;
            if (englishPublication != null)
            {
                // Find section by SectionCode
                section = englishPublication.Sections.FirstOrDefault(s => s.SectionCode == normalizedSectionCode);
                
                if (section == null)
                {
                    // Try to find by SectionCode as fallback (case-insensitive)
                    section = englishPublication.Sections.FirstOrDefault(s => 
                        s.SectionCode.Equals(normalizedSectionCode, StringComparison.OrdinalIgnoreCase));
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
            foreach (var (languageCode, _) in languages)
            {
                await SeedLanguageForSection(db, publicationCode, normalizedSectionCode, languageCode);
            }
        }

        await db.SaveChangesAsync();
        logger.Debug("Seeded section languages");

        // Seed sections for publications without language (LanguageId == null) - data-driven, not hard-coded
        await SeedSectionsWithoutLanguage(db);
        await db.SaveChangesAsync();
        logger.Information("Seeded section languages for publications without language");
    }

    /// <summary>
    /// Adds Spanish (S) to SectionLanguages for every (publication, section) that has English (E).
    /// Enables EnsureAllSectionsForPublicationAsync(pub, "S") to discover and fetch sections for Spanish.
    /// Call after SyncPublicationLanguagesForSpanishAsync so PublicationLanguage rows for S exist.
    /// </summary>
    public async Task SyncSectionLanguagesForSpanishAsync(MediaDbContext db)
    {
        const string SpanishCode = "S";
        var spanishLanguage = await languageSeeder.GetOrCreateLanguageByCode(db, SpanishCode);

        var sectionLanguagesE = await db.SectionLanguages
            .Include(sl => sl.Language)
            .Include(sl => sl.PublicationLanguage)
            .Where(sl => sl.Language != null && sl.Language.LanguageCode == AppConstants.Media.DefaultLanguageCode)
            .ToListAsync();

        var publicationLanguagesS = await db.PublicationLanguages
            .Include(pl => pl.Language)
            .Where(pl => pl.Language != null && pl.Language.LanguageCode == SpanishCode)
            .ToDictionaryAsync(pl => pl.PublicationCode, pl => pl, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var slE in sectionLanguagesE)
        {
            if (!publicationLanguagesS.TryGetValue(slE.PublicationCode, out var plS))
            {
                continue;
            }

            var exists = await db.SectionLanguages
                .AnyAsync(sl => sl.PublicationCode == slE.PublicationCode
                    && sl.SectionCode == slE.SectionCode
                    && sl.LanguageId == spanishLanguage.Id);
            if (exists)
            {
                continue;
            }

            db.SectionLanguages.Add(new SectionLanguage
            {
                PublicationCode = slE.PublicationCode,
                SectionCode = slE.SectionCode,
                Language = spanishLanguage,
                LanguageId = spanishLanguage.Id,
                PublicationLanguage = plS,
                PublicationLanguageId = plS.Id
            });
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync();
            logger.Information("SyncSectionLanguagesForSpanish: Added Spanish (S) for {Count} section(s)", added);
        }
    }

    private async Task SeedLanguageForSection(MediaDbContext db, string publicationCode, string sectionCode, string languageCode)
    {
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = publicationCode.ToLowerInvariant();
        var normalizedSectionCode = sectionCode.ToLowerInvariant();

        var publicationCodeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(normalizedPublicationCode) ?? normalizedPublicationCode;

        // Get or create language
        var language = await languageSeeder.GetOrCreateLanguageByCode(db, normalizedLanguageCode);
        
        // Get the PublicationLanguage for this publication and language
        // Check both in database and in the current context (uncommitted changes)
        var publicationLanguage = await db.PublicationLanguages
            .FirstOrDefaultAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == language.Id);

        if (publicationLanguage == null)
        {
            // Also check if it's being tracked in the context but not yet saved
            publicationLanguage = db.ChangeTracker.Entries<PublicationLanguage>()
                .Where(e => e.Entity.PublicationCode == publicationCodeForDb && e.Entity.LanguageId == language.Id)
                .Select(e => e.Entity)
                .FirstOrDefault();

            if (publicationLanguage == null)
            {
                logger.Warning("PublicationLanguage not found for {PublicationCode} and {LanguageCode}, creating it", publicationCodeForDb, languageCode);
                
                // Determine catalog type and category based on publication code
                var catalogType = PublicationTypeHelper.GetCatalogType(normalizedPublicationCode);
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
                    // Can't create SectionLanguage without PublicationLanguage
                    return;
                }

                var isMusic = !JwSourceHelper.IsMusicExcludedPublicationCodes.Contains(publicationCode) &&
                    (string.Equals(categoryCode, "Music", StringComparison.OrdinalIgnoreCase) ||
                     JwSourceHelper.IsMusicPublicationCode(publicationCode));

                publicationLanguage = new PublicationLanguage
                {
                    PublicationCode = publicationCodeForDb, // Use case-sensitive code for dramas
                    Language = language,
                    CatalogType = catalogType,
                    Category = category,
                    CategoryId = category.Id,
                    IsMusic = isMusic
                };
                db.PublicationLanguages.Add(publicationLanguage);
                // Save to get the ID
                await db.SaveChangesAsync();
            }
        }
        
        // Check if already exists (use case-sensitive code for dramas)
        var exists = await db.SectionLanguages
            .AnyAsync(sl => sl.PublicationCode == publicationCodeForDb && 
                           sl.SectionCode == normalizedSectionCode && 
                           sl.LanguageId == language.Id);

        if (!exists)
        {
            var sectionLanguage = new SectionLanguage
            {
                PublicationCode = publicationCodeForDb, // Use case-sensitive code for dramas
                SectionCode = normalizedSectionCode,
                Language = language,
                PublicationLanguage = publicationLanguage
            };
            db.SectionLanguages.Add(sectionLanguage);
        }
    }

    /// <summary>
    /// Seeds SectionLanguage entries for sections of publications without language (LanguageId == null).
    /// This is data-driven - finds all sections of publications in BiblePublications with LanguageId == null
    /// and creates corresponding SectionLanguage entries with LanguageId == null.
    /// </summary>
    private async Task SeedSectionsWithoutLanguage(MediaDbContext db)
    {
        // Get all publications without language (LanguageId == null) with their sections - data-driven, not hard-coded
        var publicationsWithoutLanguage = await db.BiblePublications
            .AsNoTracking()
            .Include(bp => bp.BiblePublicationCategories)
            .ThenInclude(bpc => bpc.Category)
            .Include(bp => bp.Sections)
            .Where(bp => bp.LanguageId == null)
            .ToListAsync();

        if (publicationsWithoutLanguage.Count == 0)
        {
            logger.Debug("No publications without language found to seed sections");
            return;
        }

        logger.Information("Seeding SectionLanguage entries for sections of {Count} publication(s) without language", publicationsWithoutLanguage.Count);

        foreach (var publication in publicationsWithoutLanguage)
        {
            if (publication.PrimaryCategory == null)
            {
                logger.Warning("Category not found for publication {PublicationCode} without language, skipping", publication.PublicationCode);
                continue;
            }

            // Normalize publication code for database (case-sensitive for dramas)
            var normalizedPublicationCode = publication.PublicationCode.ToLowerInvariant();
            var publicationCodeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(normalizedPublicationCode) ?? normalizedPublicationCode;

            // Get or create PublicationLanguage entry with LanguageId == null for this publication
            var publicationLanguage = await db.PublicationLanguages
                .FirstOrDefaultAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == null);

            if (publicationLanguage == null)
            {
                // Also check if it's being tracked in the context but not yet saved
                publicationLanguage = db.ChangeTracker.Entries<PublicationLanguage>()
                    .Where(e => e.Entity.PublicationCode == publicationCodeForDb && e.Entity.LanguageId == null)
                    .Select(e => e.Entity)
                    .FirstOrDefault();

                if (publicationLanguage == null)
                {
                    logger.Warning("PublicationLanguage with LanguageId == null not found for {PublicationCode}, creating it", publicationCodeForDb);

                    var categoryId = publication.PrimaryCategoryId;
                    var category = await db.Categories.FindAsync(categoryId);
                    if (category == null)
                    {
                        logger.Warning("Category Id {CategoryId} not found for publication {PublicationCode}, skipping", categoryId, publicationCodeForDb);
                        continue;
                    }

                    var catalogType = PublicationTypeHelper.GetCatalogType(normalizedPublicationCode);

                    publicationLanguage = new PublicationLanguage
                    {
                        PublicationCode = publicationCodeForDb,
                        LanguageId = null,
                        Language = null,
                        CatalogType = catalogType,
                        Category = category,
                        CategoryId = category.Id,
                        IsMusic = publication.IsMusic
                    };
                    db.PublicationLanguages.Add(publicationLanguage);
                    await db.SaveChangesAsync();
                }
            }

            // Seed sections for this publication without language
            if (publication.Sections == null || publication.Sections.Count == 0)
            {
                logger.Debug("No sections found for publication {PublicationCode} without language", publicationCodeForDb);
                continue;
            }

            foreach (var section in publication.Sections)
            {
                var normalizedSectionCode = section.SectionCode.ToLowerInvariant();

                // Check if already exists (with LanguageId == null)
                var exists = await db.SectionLanguages
                    .AnyAsync(sl => sl.PublicationCode == publicationCodeForDb && 
                                   sl.SectionCode == normalizedSectionCode && 
                                   sl.LanguageId == null);

                if (!exists)
                {
                    var sectionLanguage = new SectionLanguage
                    {
                        PublicationCode = publicationCodeForDb,
                        SectionCode = normalizedSectionCode,
                        LanguageId = null, // No language FK for sections of publications without language
                        Language = null,
                        PublicationLanguage = publicationLanguage,
                        PublicationLanguageId = publicationLanguage.Id
                    };
                    db.SectionLanguages.Add(sectionLanguage);
                    logger.Debug("Added SectionLanguage entry for {PublicationCode}/{SectionCode} without language",
                        publicationCodeForDb, normalizedSectionCode);
                }
            }
        }
    }
}
