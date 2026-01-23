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
    private readonly DramaFetcher dramaFetcher;
    private readonly FlatPublicationFetcher flatPublicationFetcher;
    private readonly EnglishSectionFetcher sectionFetcher;
    private readonly EnglishPublicationBuilder publicationBuilder;

    public EnglishContentSeeder(
        IServiceScopeFactory scopeFactory,
        HttpClient httpClient,
        ILogger logger,
        DramaFetcher dramaFetcher,
        FlatPublicationFetcher flatPublicationFetcher)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.dramaFetcher = dramaFetcher ?? throw new ArgumentNullException(nameof(dramaFetcher));
        this.flatPublicationFetcher = flatPublicationFetcher ?? throw new ArgumentNullException(nameof(flatPublicationFetcher));
        
        var trackParser = new EnglishTrackParser(logger);
        this.sectionFetcher = new EnglishSectionFetcher(httpClient, logger, trackParser);
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

            // For "iam" (Kingdom Melodies), language should be null
            var isIam = normalizedPublicationCode.Equals("iam", StringComparison.OrdinalIgnoreCase);
            
            Language? language = null;
            if (!isIam)
            {
                // Get or create English language (only for non-iam publications)
                language = await db.Languages
                    .FirstOrDefaultAsync(l => l.LanguageCode == normalizedLanguageCode, cancellationToken);
                
                if (language == null)
                {
                    logger.Warning("English language (E) not found in database");
                    return false;
                }
            }

            // Determine category from publication code using centralized mapping
            // Get or create PublicationLanguage to determine harvest type and category
            // Use case-sensitive code for dramas when querying database
            var publicationLanguage = await db.PublicationLanguages
                .Include(pl => pl.Category)
                .Include(pl => pl.Language)
                .FirstOrDefaultAsync(
                    pl => pl.PublicationCode == publicationCodeForDb &&
                          pl.Language.LanguageCode == normalizedLanguageCode,
                    cancellationToken);

            Category category;
            Models.Enums.HarvestType harvestType;
            
            if (publicationLanguage == null)
            {
                // Create PublicationLanguage if it doesn't exist (shouldn't happen during normal flow, but handle it)
                var tempCategoryName = JwSourceHelper.GetCategoryName(publicationCode);
                if (tempCategoryName == null)
                {
                    logger.Warning("Unknown publication type for {PublicationCode}", publicationCode);
                    return false;
                }

                var tempCategory = await db.Categories
                    .FirstOrDefaultAsync(c => c.CategoryName == tempCategoryName, cancellationToken);
                
                if (tempCategory == null)
                {
                    logger.Warning("Category {CategoryName} not found in database", tempCategoryName);
                    return false;
                }

                category = tempCategory;
                harvestType = PublicationTypeHelper.GetHarvestType(normalizedPublicationCode);
                
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
                    PublicationCode = normalizedPublicationCode,
                    Language = englishLanguage,
                    HarvestType = harvestType,
                    Category = category,
                    CategoryId = category.Id
                };
                db.PublicationLanguages.Add(publicationLanguage);
                await db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                category = publicationLanguage.Category;
                harvestType = publicationLanguage.HarvestType ?? PublicationTypeHelper.GetHarvestType(normalizedPublicationCode);
            }

            var categoryName = category.CategoryName;
            // Determine if this is a video (videos are in Dramas category but have IsVideo=true)
            var isVideo = JwSourceHelper.VideoPublicationCodes.Contains(normalizedPublicationCode);

            // Check if publication already exists
            // For "iam", check for null language; for others, check for English language
            // Use case-sensitive code for dramas
            var existingPublication = isIam
                ? await db.BiblePublications
                    .Include(bp => bp.Language)
                    .FirstOrDefaultAsync(
                        bp => bp.PublicationCode == publicationCodeForDb &&
                              bp.Language == null,
                        cancellationToken)
                : await db.BiblePublications
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

            // Use HarvestType from PublicationLanguage to determine fetching method
            switch (harvestType)
            {
                case Models.Enums.HarvestType.Sectioned:
                {
                    List<string> sectionCodes;
                
                if (normalizedPublicationCode.Equals("iam", StringComparison.OrdinalIgnoreCase))
                {
                    // For "iam" (Kingdom Melodies), use hardcoded disc codes (iam-1 to iam-9)
                    // Note: iam doesn't have language discovery, so SectionLanguages won't have entries
                    sectionCodes = Enumerable.Range(1, 9).Select(i => $"iam-{i}").ToList();
                }
                else
                {
                    // For Bible publications, use hardcoded book numbers 1-66
                    sectionCodes = Enumerable.Range(1, 66).Select(i => i.ToString()).ToList();
                }

                    // Use the existing FetchPublicationSectionsAsync logic but adapted for English
                    // For "iam", language will be null
                    return await FetchEnglishPublicationSectionsAsync(
                        db, publicationCodeForDb, normalizedLanguageCode, language, category, 
                        categoryName, isVideo, sectionCodes, cancellationToken);
                }

                case Models.Enums.HarvestType.MediatorSectioned:
                    // Drama publications use Mediator API
                    if (language == null)
                    {
                        logger.Warning("Language is null for drama publication {PublicationCode}", publicationCode);
                        return false;
                    }
                    return await dramaFetcher.FetchEnglishDramaPublicationAsync(
                        db, publicationCodeForDb, normalizedLanguageCode, language, category, cancellationToken);

                case Models.Enums.HarvestType.Flat:
                default:
                    // Music and Video use flat-track fetching
                    // Note: language is never null here since "iam" has sections (handled above)
                    if (language == null)
                    {
                        logger.Warning("Language is null for publication {PublicationCode} which should have flat tracks", publicationCode);
                        return false;
                    }
                    
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
        var isIamPublication = normalizedPublicationCode.Equals("iam", StringComparison.OrdinalIgnoreCase);

        var (sections, localizedPubName) = await sectionFetcher.FetchSectionsAsync(
            db, normalizedPublicationCode, normalizedLanguageCode, categoryName, sectionCodes, cancellationToken);

        return await publicationBuilder.BuildAndSavePublicationAsync(
            db, normalizedPublicationCode, localizedPubName, language, category,
            isVideo, isBible, isIamPublication, sections, cancellationToken);
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
            return await dramaFetcher.FetchEnglishDramaPublicationAsync(
                db, normalizedPublicationCode, normalizedLanguageCode, language, category, cancellationToken);
        }

        // Unified flat-track fetching for Music and Video (both use same GETPUBMEDIALINKS pattern)
        // Both are flat-track publications (no sections), only differ by file format (MP3 vs MP4)
        var isMusic = categoryName.Equals("Music", StringComparison.OrdinalIgnoreCase);
        var fileFormat = isVideo ? "MP4" : "MP3";
        var trackParam = isVideo ? "&track=" : "";

        // Create a temporary English publication object for the unified method
        var tempEnglishPublication = new BiblePublication
        {
            PublicationCode = normalizedPublicationCode,
            Name = normalizedPublicationCode,
            Category = category,
            IsVideo = isVideo
        };

        return await flatPublicationFetcher.FetchFlatPublicationTracksAsync(
            db, normalizedPublicationCode, normalizedLanguageCode, tempEnglishPublication,
            isVideo, isMusic, fileFormat, trackParam, language, cancellationToken);
    }
}
