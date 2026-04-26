#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Helper class for seeding English content publications.
/// </summary>
internal sealed class EnglishContentSeeder
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly HttpClient httpClient;
    private readonly ILogger logger;
    private readonly MediatorFetcher mediatorFetcher;
    private readonly FlatPublicationFetcher flatPublicationFetcher;
    private readonly EnglishSectionFetcher sectionFetcher;
    private readonly EnglishPublicationBuilder publicationBuilder;

    public EnglishContentSeeder(
        IServiceScopeFactory scopeFactory,
        HttpClient httpClient,
        ILogger logger,
        MediatorFetcher mediatorFetcher,
        FlatPublicationFetcher flatPublicationFetcher)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.mediatorFetcher = mediatorFetcher ?? throw new ArgumentNullException(nameof(mediatorFetcher));
        this.flatPublicationFetcher = flatPublicationFetcher ?? throw new ArgumentNullException(nameof(flatPublicationFetcher));
        
        this.sectionFetcher = new EnglishSectionFetcher(httpClient, logger);
        this.publicationBuilder = new EnglishPublicationBuilder(logger);
    }

    public async Task<bool> SeedEnglishPublicationAsync(
        string publicationCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            const string EnglishCode = "E";
            var normalizedLanguageCode = EnglishCode.ToUpperInvariant();

            var publicationCodeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(normalizedPublicationCode) ?? normalizedPublicationCode;

            // IssueSectioned (magazine) publications with no discovered sections have nothing to seed.
            // Skip early to avoid creating orphan PublicationLanguage entries that would cause
            // the UI to show placeholders and retry loops for years with no content.
            if (MagazineHelper.IsMagazinePublicationCode(normalizedPublicationCode))
            {
                var hasSectionLanguages = await db.SectionLanguages
                    .AsNoTracking()
                    .AnyAsync(sl => sl.PublicationCode == publicationCodeForDb &&
                                    sl.Language != null &&
                                    sl.Language.LanguageCode == normalizedLanguageCode, cancellationToken);
                if (!hasSectionLanguages)
                {
                    logger.Information("No SectionLanguage entries for magazine {PublicationCode} in English (no issues discovered). Skipping.", publicationCode);
                    return true;
                }
            }

            // Data-driven check: Determine if publication has LanguageId == null
            // Check both BiblePublications and PublicationLanguages to determine if this publication needs a language
            var publicationWithoutLanguage = await db.BiblePublications
                .AsNoTracking()
                .AnyAsync(bp => bp.PublicationCode == publicationCodeForDb && bp.LanguageId == null, cancellationToken);
            
            // Also check PublicationLanguages for entries with LanguageId == null
            if (!publicationWithoutLanguage)
            {
                publicationWithoutLanguage = await db.PublicationLanguages
                    .AsNoTracking()
                    .AnyAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == null, cancellationToken);
            }
            
            Language? language = null;
            if (!publicationWithoutLanguage)
            {
                // Get or create English language (only for publications that have a language)
                language = await db.Languages
                    .FirstOrDefaultAsync(l => l.LanguageCode == normalizedLanguageCode, cancellationToken);
                
                if (language == null)
                {
                    logger.Warning("English language (E) not found in database");
                    return false;
                }
            }
            else
            {
                // Publication has LanguageId == null - skip English seeding (it doesn't have English content)
                logger.Warning("Publication {PublicationCode} has LanguageId == null, skipping English seeding (no English content)", publicationCode);
                return false;
            }

            // Determine category from publication code using centralized mapping
            // Get or create PublicationLanguage to determine catalog type and category
            // Use case-sensitive code for dramas when querying database
            var publicationLanguage = await db.PublicationLanguages
                .Include(pl => pl.Category)
                .Include(pl => pl.Language)
                .FirstOrDefaultAsync(
                    pl => pl.PublicationCode == publicationCodeForDb &&
                          pl.Language != null &&
                          pl.Language.LanguageCode == normalizedLanguageCode,
                    cancellationToken);

            Category category;
            Models.Enums.CatalogType catalogType;
            
            if (publicationLanguage == null)
            {
                // Create PublicationLanguage if it doesn't exist (shouldn't happen during normal flow, but handle it)
                var tempCategoryCode = JwSourceHelper.GetCategoryCode(publicationCode);
                if (tempCategoryCode == null)
                {
                    logger.Warning("Unknown publication type for {PublicationCode}", publicationCode);
                    return false;
                }

                var tempCategory = await db.Categories
                    .FirstOrDefaultAsync(c => c.CategoryCode == tempCategoryCode, cancellationToken);
                
                if (tempCategory == null)
                {
                    logger.Warning("Category {CategoryCode} not found in database", tempCategoryCode);
                    return false;
                }

                category = tempCategory;
                catalogType = PublicationTypeHelper.GetCatalogType(normalizedPublicationCode);
                
                // Get or create English language
                var englishLanguage = await db.Languages
                    .FirstOrDefaultAsync(l => l.LanguageCode == normalizedLanguageCode, cancellationToken);
                
                if (englishLanguage == null)
                {
                    logger.Warning("English language not found in database");
                    return false;
                }

                publicationLanguage = new PublicationLanguage
                {
                    PublicationCode = publicationCodeForDb, // Use case-sensitive code for dramas
                    Language = englishLanguage,
                    CatalogType = catalogType,
                    Category = category,
                    CategoryId = category.Id
                };
                db.PublicationLanguages.Add(publicationLanguage);
                await db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                category = publicationLanguage.Category;
                catalogType = publicationLanguage.CatalogType ?? PublicationTypeHelper.GetCatalogType(normalizedPublicationCode);
            }

            var categoryName = category.CategoryCode;
            var isVideo = PublicationTypeHelper.IsVideo(normalizedPublicationCode);

            // Check if publication already exists for English
            // Use case-sensitive code for dramas
            var existingPublication = await db.BiblePublications
                .Include(bp => bp.Language)
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.LanguageCode == normalizedLanguageCode,
                    cancellationToken);

            if (existingPublication != null)
            {
                logger.Information("English publication {PublicationCode} already exists, skipping", publicationCode);
                return true;
            }

            // Use CatalogType from PublicationLanguage to determine fetching method
            switch (catalogType)
            {
                case Models.Enums.CatalogType.Sectioned:
                {
                    List<string> sectionCodes;
                
                    // Data-driven: Get section codes from BiblePublications if already cataloged, or use defaults
                    // Check if publication already exists with sections
                    var existingPubWithSections = await db.BiblePublications
                        .AsNoTracking()
                        .Include(bp => bp.Sections)
                        .FirstOrDefaultAsync(
                            bp => bp.PublicationCode == publicationCodeForDb &&
                                  (bp.LanguageId == null || (bp.Language != null && bp.Language.LanguageCode == normalizedLanguageCode)),
                            cancellationToken);
                    
                    if (existingPubWithSections?.Sections != null && existingPubWithSections.Sections.Count > 0)
                    {
                        // Use section codes from existing publication (data-driven)
                        sectionCodes = existingPubWithSections.Sections
                            .OrderBy(s => s.SectionCode, SectionCodeHelper.SectionCodeComparer)
                            .Select(s => s.SectionCode)
                            .ToList();
                        logger.Debug("Using {Count} section codes from existing publication {PublicationCode}", 
                            sectionCodes.Count, publicationCode);
                    }
                    else
                    {
                        // Fallback: Use default section codes based on category
                        // For Bible category, use book numbers 1-66
                        // For Music category with sectioned structure, query from database or use discovery
                        if (categoryName.Equals("Bible", StringComparison.OrdinalIgnoreCase))
                        {
                            sectionCodes = Enumerable.Range(1, 66).Select(i => i.ToString()).ToList();
                        }
                        else
                        {
                            // For other sectioned publications, try to get from SectionLanguages or use a default range
                            // This is a fallback - ideally sections should be discovered during catalog phase
                            logger.Warning("No existing sections found for publication {PublicationCode}, using default section codes", publicationCode);
                            // Use a reasonable default - for music, typically 1-9 discs
                            sectionCodes = Enumerable.Range(1, 9).Select(i => $"{normalizedPublicationCode}-{i}").ToList();
                        }
                    }

                    // Use the existing FetchPublicationSectionsAsync logic but adapted for English
                    return await FetchEnglishPublicationSectionsAsync(
                        db, publicationCodeForDb, normalizedLanguageCode, language, category, 
                        categoryName, isVideo, sectionCodes, cancellationToken);
                }

                case Models.Enums.CatalogType.IssueSectioned:
                {
                    var issueSectionCodes = await db.SectionLanguages
                        .AsNoTracking()
                        .Where(sl => sl.PublicationCode == publicationCodeForDb &&
                                     sl.Language != null &&
                                     sl.Language.LanguageCode == normalizedLanguageCode)
                        .Select(sl => sl.SectionCode)
                        .OrderBy(sc => sc)
                        .ToListAsync(cancellationToken);

                    if (issueSectionCodes.Count == 0)
                    {
                        logger.Information("No SectionLanguage entries found for IssueSectioned publication {PublicationCode} in English (no issues discovered for this year). Skipping.", publicationCode);
                        return true;
                    }

                    logger.Information("Found {Count} issue section codes for {PublicationCode} from SectionLanguages",
                        issueSectionCodes.Count, publicationCode);

                    return await FetchEnglishPublicationSectionsAsync(
                        db, publicationCodeForDb, normalizedLanguageCode, language, category,
                        categoryName, isVideo, issueSectionCodes, cancellationToken);
                }

                case Models.Enums.CatalogType.MediatorSectioned:
                    return await mediatorFetcher.FetchEnglishMediatorPublicationAsync(
                        db, publicationCodeForDb, normalizedLanguageCode, language, category, cancellationToken);

                case Models.Enums.CatalogType.Flat:
                default:
                    return await FetchEnglishPublicationTracksAsync(
                        db, publicationCodeForDb, normalizedLanguageCode, language, category, 
                        categoryName, isVideo, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error seeding English publication {PublicationCode}", publicationCode);
            return false;
        }
    }

    private async Task<bool> FetchEnglishPublicationSectionsAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        Language? language,
        Category category,
        string categoryName,
        bool isVideo,
        List<string> sectionCodes,
        CancellationToken cancellationToken)
    {
        var isBible = categoryName.Equals("Bible", StringComparison.OrdinalIgnoreCase);
        
        // Data-driven: Check if publication has LanguageId == null (determines if it's instrumental music)
        var publicationWithoutLanguage = await db.BiblePublications
            .AsNoTracking()
            .AnyAsync(bp => bp.PublicationCode == normalizedPublicationCode && bp.LanguageId == null, cancellationToken);
        
        // Also check PublicationLanguages for entries with LanguageId == null
        if (!publicationWithoutLanguage)
        {
            publicationWithoutLanguage = await db.PublicationLanguages
                .AsNoTracking()
                .AnyAsync(pl => pl.PublicationCode == normalizedPublicationCode && pl.LanguageId == null, cancellationToken);
        }

        var (sections, localizedPubName) = await sectionFetcher.FetchSectionsAsync(
            db, normalizedPublicationCode, normalizedLanguageCode, categoryName, sectionCodes, isVideo, cancellationToken);

        return await publicationBuilder.BuildAndSavePublicationAsync(
            db, normalizedPublicationCode, localizedPubName, language, category,
            isVideo, isBible, publicationWithoutLanguage, sections, cancellationToken);
    }

    private async Task<bool> FetchEnglishPublicationTracksAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        Language language,
        Category category,
        string categoryName,
        bool isVideo,
        CancellationToken cancellationToken)
    {
        // Check if this is a drama (uses Mediator API, not GETPUBMEDIALINKS)
        var isDrama = PublicationTypeHelper.IsDrama(normalizedPublicationCode);
        
        if (isDrama)
        {
            return await mediatorFetcher.FetchEnglishMediatorPublicationAsync(
                db, normalizedPublicationCode, normalizedLanguageCode, language, category, cancellationToken);
        }

        // Unified flat-track fetching for Music and Video (both use same GETPUBMEDIALINKS pattern)
        // Both are flat-track publications (no sections), only differ by file format (MP3 vs MP4)
        var isMusic = categoryName.Equals("Music", StringComparison.OrdinalIgnoreCase);
        var fileFormat = isVideo ? "MP4" : "MP3";
        var tempEnglishPublication = new BiblePublication
        {
            PublicationCode = normalizedPublicationCode,
            Name = normalizedPublicationCode,
            BiblePublicationCategories = new List<BiblePublicationCategory> { new BiblePublicationCategory { BiblePublicationId = 0, CategoryId = category.Id, Category = category } },
            IsVideo = isVideo
        };

        return await flatPublicationFetcher.FetchFlatPublicationTracksAsync(
            db, normalizedPublicationCode, normalizedLanguageCode, tempEnglishPublication,
            isVideo, isMusic, fileFormat, language, cancellationToken);
    }
}
