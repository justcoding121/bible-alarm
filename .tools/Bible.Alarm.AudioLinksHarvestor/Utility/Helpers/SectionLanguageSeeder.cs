#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.AudioLinksHarvestor.Models;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Utility.Helpers;

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

    public async Task SeedSectionLanguages(MediaDbContext db)
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
            BiblePublicationSection? section = null;
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
                
                publicationLanguage = new PublicationLanguage
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
            var sectionLanguage = new SectionLanguage
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
}
