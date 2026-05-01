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
using Bible.Alarm.Shared.Models.Enums;
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
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.mediatorFetcher = mediatorFetcher ?? throw new ArgumentNullException(nameof(mediatorFetcher));
        this.flatPublicationFetcher = flatPublicationFetcher ?? throw new ArgumentNullException(nameof(flatPublicationFetcher));

        sectionFetcher = new EnglishSectionFetcher(httpClient, logger);
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
            var normalizedLanguageCode = AppConstants.Media.DefaultLanguageCode.ToUpperInvariant();
            var publicationCodeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(normalizedPublicationCode) ?? normalizedPublicationCode;

            if (await ShouldSkipMagazineWithoutEnglishSectionsAsync(
                    db, normalizedPublicationCode, publicationCodeForDb, normalizedLanguageCode, publicationCode, cancellationToken))
            {
                return true;
            }

            if (await PublicationRowsIndicateNoLanguageAsync(db, publicationCodeForDb, cancellationToken))
            {
                logger.Warning("Publication {PublicationCode} has LanguageId == null, skipping English seeding (no English content)", publicationCode);
                return false;
            }

            var language = await db.Languages
                .FirstOrDefaultAsync(l => l.LanguageCode == normalizedLanguageCode, cancellationToken);

            if (language == null)
            {
                logger.Warning("English language (E) not found in database");
                return false;
            }

            var categoryResolved = await EnsureCategoryAndCatalogTypeAsync(
                db, publicationCode, publicationCodeForDb, normalizedPublicationCode, normalizedLanguageCode, cancellationToken);

            if (!categoryResolved.HasValue)
            {
                return false;
            }

            var (category, catalogType) = categoryResolved.Value;
            var categoryName = category.CategoryCode;
            var isVideo = PublicationTypeHelper.IsVideo(normalizedPublicationCode);

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

            return await SeedEnglishByCatalogTypeAsync(
                db,
                catalogType,
                publicationCode,
                publicationCodeForDb,
                normalizedPublicationCode,
                normalizedLanguageCode,
                language,
                category,
                categoryName,
                isVideo,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error seeding English publication {PublicationCode}", publicationCode);
            return false;
        }
    }

    private async Task<bool> ShouldSkipMagazineWithoutEnglishSectionsAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string publicationCodeForDb,
        string normalizedLanguageCode,
        string publicationCode,
        CancellationToken cancellationToken)
    {
        if (!MagazineHelper.IsMagazinePublicationCode(normalizedPublicationCode))
        {
            return false;
        }

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

        return false;
    }

    private static async Task<bool> PublicationRowsIndicateNoLanguageAsync(
        MediaDbContext db,
        string publicationCodeForDb,
        CancellationToken cancellationToken)
    {
        var fromBp = await db.BiblePublications
            .AsNoTracking()
            .AnyAsync(bp => bp.PublicationCode == publicationCodeForDb && bp.LanguageId == null, cancellationToken);

        if (fromBp)
        {
            return true;
        }

        return await db.PublicationLanguages
            .AsNoTracking()
            .AnyAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == null, cancellationToken);
    }

    private async Task<(Category category, CatalogType catalogType)?> EnsureCategoryAndCatalogTypeAsync(
        MediaDbContext db,
        string publicationCode,
        string publicationCodeForDb,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        CancellationToken cancellationToken)
    {
        var publicationLanguage = await db.PublicationLanguages
            .Include(pl => pl.Category)
            .Include(pl => pl.Language)
            .FirstOrDefaultAsync(
                pl => pl.PublicationCode == publicationCodeForDb &&
                      pl.Language != null &&
                      pl.Language.LanguageCode == normalizedLanguageCode,
                cancellationToken);

        if (publicationLanguage != null)
        {
            var catalogType = publicationLanguage.CatalogType ?? PublicationTypeHelper.GetCatalogType(normalizedPublicationCode);
            return (publicationLanguage.Category, catalogType);
        }

        var tempCategoryCode = JwSourceHelper.GetCategoryCode(publicationCode);
        if (tempCategoryCode == null)
        {
            logger.Warning("Unknown publication type for {PublicationCode}", publicationCode);
            return null;
        }

        var tempCategory = await db.Categories
            .FirstOrDefaultAsync(c => c.CategoryCode == tempCategoryCode, cancellationToken);

        if (tempCategory == null)
        {
            logger.Warning("Category {CategoryCode} not found in database", tempCategoryCode);
            return null;
        }

        var createdCatalogType = PublicationTypeHelper.GetCatalogType(normalizedPublicationCode);

        var englishLanguage = await db.Languages
            .FirstOrDefaultAsync(l => l.LanguageCode == normalizedLanguageCode, cancellationToken);

        if (englishLanguage == null)
        {
            logger.Warning("English language not found in database");
            return null;
        }

        var newPublicationLanguage = new PublicationLanguage
        {
            PublicationCode = publicationCodeForDb,
            Language = englishLanguage,
            CatalogType = createdCatalogType,
            Category = tempCategory,
            CategoryId = tempCategory.Id
        };

        db.PublicationLanguages.Add(newPublicationLanguage);
        await db.SaveChangesAsync(cancellationToken);

        return (tempCategory, createdCatalogType);
    }

    private async Task<bool> SeedEnglishByCatalogTypeAsync(
        MediaDbContext db,
        CatalogType catalogType,
        string publicationCode,
        string publicationCodeForDb,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        Language language,
        Category category,
        string categoryName,
        bool isVideo,
        CancellationToken cancellationToken)
    {
        switch (catalogType)
        {
            case CatalogType.Sectioned:
                var sectionCodes = await ResolveEnglishSectionCodesForSectionedCatalogAsync(
                    db,
                    publicationCode,
                    publicationCodeForDb,
                    normalizedPublicationCode,
                    normalizedLanguageCode,
                    categoryName,
                    cancellationToken);

                return await FetchEnglishPublicationSectionsAsync(new FetchEnglishPublicationSectionsRequest(
                    db, publicationCodeForDb, normalizedLanguageCode, language,
                    categoryName, isVideo, sectionCodes, cancellationToken));

            case CatalogType.IssueSectioned:
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

                return await FetchEnglishPublicationSectionsAsync(new FetchEnglishPublicationSectionsRequest(
                    db, publicationCodeForDb, normalizedLanguageCode, language,
                    categoryName, isVideo, issueSectionCodes, cancellationToken));

            case CatalogType.MediatorSectioned:
                return await mediatorFetcher.FetchEnglishMediatorPublicationAsync(
                    db, publicationCodeForDb, normalizedLanguageCode, language, cancellationToken);

            case CatalogType.Flat:
            default:
                return await FetchEnglishPublicationTracksAsync(new FetchEnglishPublicationTracksRequest(
                    db, publicationCodeForDb, normalizedLanguageCode, language, category,
                    categoryName, isVideo, cancellationToken));
        }
    }

    private async Task<List<string>> ResolveEnglishSectionCodesForSectionedCatalogAsync(
        MediaDbContext db,
        string publicationCode,
        string publicationCodeForDb,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        string categoryName,
        CancellationToken cancellationToken)
    {
        var existingPubWithSections = await db.BiblePublications
            .AsNoTracking()
            .Include(bp => bp.Sections)
            .FirstOrDefaultAsync(
                bp => bp.PublicationCode == publicationCodeForDb &&
                      (bp.LanguageId == null || (bp.Language != null && bp.Language.LanguageCode == normalizedLanguageCode)),
                cancellationToken);

        if (existingPubWithSections?.Sections != null && existingPubWithSections.Sections.Count > 0)
        {
            var codes = existingPubWithSections.Sections
                .OrderBy(s => s.SectionCode, SectionCodeHelper.SectionCodeComparer)
                .Select(s => s.SectionCode)
                .ToList();
            logger.Debug("Using {Count} section codes from existing publication {PublicationCode}",
                codes.Count, publicationCode);
            return codes;
        }

        if (categoryName.Equals(AppConstants.Media.BiblePublicationCategoryBible, StringComparison.OrdinalIgnoreCase))
        {
            return Enumerable.Range(1, 66).Select(i => i.ToString()).ToList();
        }

        logger.Warning("No existing sections found for publication {PublicationCode}, using default section codes", publicationCode);
        return Enumerable.Range(1, 9).Select(i => $"{normalizedPublicationCode}-{i}").ToList();
    }

    private async Task<bool> FetchEnglishPublicationSectionsAsync(FetchEnglishPublicationSectionsRequest req)
    {
        var db = req.Db;
        var normalizedPublicationCode = req.NormalizedPublicationCode;
        var normalizedLanguageCode = req.NormalizedLanguageCode;
        var language = req.Language;
        var categoryName = req.CategoryName;
        var isVideo = req.IsVideo;
        var sectionCodes = req.SectionCodes;
        var cancellationToken = req.CancellationToken;

        var isBible = categoryName.Equals(AppConstants.Media.BiblePublicationCategoryBible, StringComparison.OrdinalIgnoreCase);
        
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

        var (sections, localizedPubName) = await sectionFetcher.FetchSectionsAsync(new FetchEnglishSectionsRequest(
            db, normalizedPublicationCode, normalizedLanguageCode, categoryName, sectionCodes, isVideo, cancellationToken));

        return await publicationBuilder.BuildAndSavePublicationAsync(new BuildEnglishPublicationRequest(
            db,
            normalizedPublicationCode,
            localizedPubName,
            language,
            isVideo,
            isBible,
            publicationWithoutLanguage,
            sections,
            cancellationToken));
    }

    private async Task<bool> FetchEnglishPublicationTracksAsync(FetchEnglishPublicationTracksRequest req)
    {
        var db = req.Db;
        var normalizedPublicationCode = req.NormalizedPublicationCode;
        var normalizedLanguageCode = req.NormalizedLanguageCode;
        var language = req.Language;
        var category = req.Category;
        var categoryName = req.CategoryName;
        var isVideo = req.IsVideo;
        var cancellationToken = req.CancellationToken;

        // Check if this is a drama (uses Mediator API, not GETPUBMEDIALINKS)
        var isDrama = PublicationTypeHelper.IsDrama(normalizedPublicationCode);
        
        if (isDrama)
        {
            return await mediatorFetcher.FetchEnglishMediatorPublicationAsync(
                db, normalizedPublicationCode, normalizedLanguageCode, language, cancellationToken);
        }

        // Unified flat-track fetching for Music and Video (both use same GETPUBMEDIALINKS pattern)
        // Both are flat-track publications (no sections), only differ by file format (MP3 vs MP4)
        var isMusic = categoryName.Equals(AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase);
        var fileFormat = isVideo ? AppConstants.Media.MediaStreamFormatMp4 : AppConstants.Media.MediaStreamFormatMp3;
        var tempEnglishPublication = new BiblePublication
        {
            PublicationCode = normalizedPublicationCode,
            Name = normalizedPublicationCode,
            BiblePublicationCategories = new List<BiblePublicationCategory> { new BiblePublicationCategory { BiblePublicationId = 0, CategoryId = category.Id, Category = category } },
            IsVideo = isVideo
        };

        return await flatPublicationFetcher.FetchFlatPublicationTracksAsync(new FetchFlatPublicationTracksRequest(
            db, normalizedPublicationCode, normalizedLanguageCode, tempEnglishPublication,
            isVideo, isMusic, fileFormat, language, cancellationToken));
    }
}
