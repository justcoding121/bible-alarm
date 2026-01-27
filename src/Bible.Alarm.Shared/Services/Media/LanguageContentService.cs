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
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for fetching and caching non-English language content on-demand.
/// When a user changes language, this service checks if the content exists in the database,
/// and if not, fetches it from the API and stores it for future use.
/// </summary>
public sealed class LanguageContentService : ILanguageContentService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;
    private readonly HttpClient httpClient;
    private readonly DramaFetcher dramaFetcher;
    private readonly VideoLocalizedNameFetcher videoLocalizedNameFetcher;
    private readonly FlatPublicationFetcher flatPublicationFetcher;
    private readonly SectionFetcher sectionFetcher;
    private readonly EnglishContentSeeder englishContentSeeder;
    private readonly PublicationEnsurer publicationEnsurer;

    public LanguageContentService(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        HttpClient httpClient)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.videoLocalizedNameFetcher = new VideoLocalizedNameFetcher(httpClient, logger);
        this.dramaFetcher = new DramaFetcher(httpClient, logger);
        this.flatPublicationFetcher = new FlatPublicationFetcher(httpClient, logger, videoLocalizedNameFetcher);
        this.sectionFetcher = new SectionFetcher(httpClient, logger);
        this.englishContentSeeder = new EnglishContentSeeder(scopeFactory, httpClient, logger, dramaFetcher, flatPublicationFetcher);
        this.publicationEnsurer = new PublicationEnsurer(scopeFactory, logger, this);
    }

    public async Task<bool> FetchPublicationTracksAsync(
        string publicationCode,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            // For dramas, use case-sensitive publication codes: "Dramas" or "DramaticBibleReadings"
            // For others (e.g., "gnj"), preserve exact case
            var lowerCode = publicationCode.ToLowerInvariant();
            var isDrama = PublicationTypeHelper.IsDrama(lowerCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = lowerCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = publicationCode; // Preserve exact case (e.g., "gnj")
            }

            // Get PublicationLanguage to determine harvest type and category
            // HarvestType is sufficient to determine if ad-hoc fetching is possible
            // Use case-sensitive code for dramas when querying database
            var publicationLanguage = await db.PublicationLanguages
                .Include(pl => pl.Language)
                .Include(pl => pl.Category)
                .FirstOrDefaultAsync(
                    pl => pl.PublicationCode == publicationCodeForDb &&
                          pl.Language != null &&
                          pl.Language.LanguageCode == normalizedLanguageCode,
                    cancellationToken);

            if (publicationLanguage == null)
            {
                logger.Warning("Language {LanguageCode} is not available for publication {PublicationCode} (not in PublicationLanguages)",
                    languageCode, publicationCode);
                return false;
            }

            // If PublicationLanguage has LanguageId == null, it means the publication doesn't have language-specific content
            // and can't be ad-hoc fetched with a language code
            if (publicationLanguage.LanguageId == null)
            {
                logger.Warning("Publication {PublicationCode} doesn't support ad-hoc fetching with language code (has LanguageId = NULL in PublicationLanguages)",
                    publicationCode);
                return false;
            }

            // Verify English publication exists (needed as template for ad-hoc fetching)
            var englishPublication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.Category)
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.LanguageCode == "E",
                    cancellationToken);

            if (englishPublication == null)
            {
                logger.Warning("English publication {PublicationCode} not found (required as template for ad-hoc fetching)", publicationCode);
                return false;
            }

            // Delete existing publication for this language (use case-sensitive code for dramas)
            var existingPublication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.Tracks)
                    .ThenInclude(t => t.UrlParams)
                .AsSplitQuery()
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.LanguageCode == normalizedLanguageCode,
                    cancellationToken);

            if (existingPublication != null)
            {
                logger.Information("Deleting existing publication {PublicationCode} for language {LanguageCode}",
                    publicationCode, languageCode);
                db.BiblePublications.Remove(existingPublication);
                await db.SaveChangesAsync(cancellationToken);
            }

            // Use HarvestType from PublicationLanguage to determine fetching method
            var category = publicationLanguage.Category;
            var categoryName = category.CategoryName;
            var isVideo = categoryName.Equals("Dramas", StringComparison.OrdinalIgnoreCase) && 
                         PublicationTypeHelper.IsVideo(lowerCode);

            var harvestType = publicationLanguage.HarvestType ?? PublicationTypeHelper.GetHarvestType(lowerCode);
            switch (harvestType)
            {
                case Models.Enums.HarvestType.MediatorSectioned:
                    // Drama publications use Mediator API
                    return await dramaFetcher.FetchDramaPublicationTracksAsync(
                        db, publicationCodeForDb, normalizedLanguageCode, englishPublication, cancellationToken);

                case Models.Enums.HarvestType.Flat:
                    // Music and Video use flat-track fetching
                    var isMusic = categoryName.Equals("Music", StringComparison.OrdinalIgnoreCase);
                    var fileFormat = isVideo ? "MP4" : "MP3";
                    var trackParam = isVideo ? "&track=" : "";

                    return await flatPublicationFetcher.FetchFlatPublicationTracksAsync(
                        db, publicationCodeForDb, normalizedLanguageCode, englishPublication,
                        isVideo, isMusic, fileFormat, trackParam, null, cancellationToken);

                case Models.Enums.HarvestType.Sectioned:
                default:
                    logger.Warning("Publication {PublicationCode} has Sectioned harvest type, use FetchPublicationSectionsAsync instead",
                        publicationCode);
                    return false;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error fetching publication tracks for {PublicationCode} in language {LanguageCode}",
                publicationCode, languageCode);
            return false;
        }
    }

    // Method moved to FlatPublicationFetcher helper class

    public async Task<bool> FetchPublicationSectionsAsync(
        string publicationCode,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            // For dramas, use case-sensitive publication codes: "Dramas" or "DramaticBibleReadings"
            // For others (e.g., "gnj"), preserve exact case
            var lowerCode = publicationCode.ToLowerInvariant();
            var isDrama = PublicationTypeHelper.IsDrama(lowerCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = lowerCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = publicationCode; // Preserve exact case (e.g., "gnj")
            }

            // Get PublicationLanguage to determine harvest type
            // HarvestType is sufficient to determine if ad-hoc fetching is possible
            // Use case-sensitive code for dramas when querying database
            var publicationLanguage = await db.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Language)
                .FirstOrDefaultAsync(
                    pl => pl.PublicationCode == publicationCodeForDb &&
                          pl.Language != null &&
                          pl.Language.LanguageCode == normalizedLanguageCode,
                    cancellationToken);

            if (publicationLanguage == null)
            {
                logger.Warning("Language {LanguageCode} is not available for publication {PublicationCode} (not in PublicationLanguages)",
                    languageCode, publicationCode);
                return false;
            }

            // If PublicationLanguage has LanguageId == null, it means the publication doesn't have language-specific content
            // and can't be ad-hoc fetched with a language code
            if (publicationLanguage.LanguageId == null)
            {
                logger.Warning("Publication {PublicationCode} doesn't support ad-hoc fetching with language code (has LanguageId = NULL in PublicationLanguages)",
                    publicationCode);
                return false;
            }

            // Verify English publication exists (needed as template for ad-hoc fetching)
            var englishPublication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.Category)
                .Include(bp => bp.Sections)
                .AsSplitQuery()
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.LanguageCode == "E",
                    cancellationToken);

            if (englishPublication == null)
            {
                logger.Warning("English publication {PublicationCode} not found (required as template for ad-hoc fetching)", publicationCode);
                return false;
            }

            // Get all section codes from SectionLanguages for this publication+language
            // Use case-sensitive code for dramas when querying database
            var sectionCodes = await db.SectionLanguages
                .Include(sl => sl.Language)
                .Where(sl => sl.PublicationCode == publicationCodeForDb &&
                           sl.Language.LanguageCode == normalizedLanguageCode)
                .Select(sl => sl.SectionCode)
                .Distinct()
                .OrderBy(sc => sc)
                .ToListAsync(cancellationToken);

            if (sectionCodes.Count == 0)
            {
                logger.Warning("No sections found for publication {PublicationCode} in language {LanguageCode}",
                    publicationCode, languageCode);
                return false;
            }

            // Delete existing publication for this language (including sections and tracks)
            // Use case-sensitive code for dramas
            var existingPublication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.Sections)
                    .ThenInclude(s => s.Tracks)
                        .ThenInclude(t => t.UrlParams)
                .Include(bp => bp.Sections)
                    .ThenInclude(s => s.UrlParams)
                .AsSplitQuery()
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.LanguageCode == normalizedLanguageCode,
                    cancellationToken);

            if (existingPublication != null)
            {
                logger.Information("Deleting existing publication {PublicationCode} for language {LanguageCode}",
                    publicationCode, languageCode);
                db.BiblePublications.Remove(existingPublication);
                await db.SaveChangesAsync(cancellationToken);
            }

            return await sectionFetcher.FetchPublicationSectionsAsync(
                db, publicationCodeForDb, normalizedLanguageCode, publicationCodeForDb,
                englishPublication, sectionCodes, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error fetching publication sections for {PublicationCode} in language {LanguageCode}",
                publicationCode, languageCode);
            return false;
        }
    }

    public async Task<bool> FetchSectionTracksAsync(
        string publicationCode,
        string sectionCode,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            var normalizedSectionCode = sectionCode.ToLowerInvariant();
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

            // Get the publication for this language
            var publication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.Category)
                .Include(bp => bp.Sections)
                .AsSplitQuery()
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.LanguageCode == normalizedLanguageCode,
                    cancellationToken);

            if (publication == null)
            {
                logger.Warning("Publication {PublicationCode} for language {LanguageCode} not found. Fetch sections first.",
                    publicationCode, languageCode);
                return false;
            }

            // Find the section - reload from database to ensure we have the correct IDs
            var section = await db.BiblePublicationSections
                .Include(s => s.UrlParams)
                .FirstOrDefaultAsync(
                    s => s.BiblePublicationId == publication.Id && s.SectionCode == normalizedSectionCode,
                    cancellationToken);

            if (section == null)
            {
                // Try to find by UrlParam as fallback - need to check sections that belong to this publication
                var allSections = await db.BiblePublicationSections
                    .Include(s => s.UrlParams)
                    .Where(s => s.BiblePublicationId == publication.Id)
                    .ToListAsync(cancellationToken);
                
                // Find section by SectionCode (UrlParams["booknum"] is only for URL construction)
                section = allSections.FirstOrDefault(s =>
                    s.SectionCode.Equals(normalizedSectionCode, StringComparison.OrdinalIgnoreCase));
            }

            if (section == null)
            {
                logger.Warning("Section {SectionCode} not found in publication {PublicationCode} for language {LanguageCode}",
                    sectionCode, publicationCode, languageCode);
                return false;
            }

            // Check if language is available for this section
            // Use case-sensitive code for dramas when querying database
            var isAvailable = await db.SectionLanguages
                .Include(sl => sl.Language)
                .AnyAsync(
                    sl => sl.PublicationCode == publicationCodeForDb &&
                          sl.SectionCode == normalizedSectionCode &&
                          sl.Language.LanguageCode == normalizedLanguageCode,
                    cancellationToken);

            if (!isAvailable)
            {
                logger.Warning("Language {LanguageCode} is not available for section {SectionCode} of publication {PublicationCode}",
                    languageCode, sectionCode, publicationCode);
                return false;
            }

            // Delete existing tracks for this section
            if (section.Tracks.Count > 0)
            {
                logger.Information("Deleting {Count} existing tracks for section {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                    section.Tracks.Count, sectionCode, publicationCode, languageCode);
                db.BiblePublicationTracks.RemoveRange(section.Tracks);
                await db.SaveChangesAsync(cancellationToken);
                section.Tracks.Clear();
            }

            return await sectionFetcher.FetchSectionTracksAsync(
                db, normalizedPublicationCode, normalizedSectionCode, normalizedLanguageCode,
                publicationCodeForDb, publication, section, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error fetching section tracks for {SectionCode} in publication {PublicationCode} for language {LanguageCode}",
                sectionCode, publicationCode, languageCode);
            return false;
        }
    }

    /// <summary>
    /// Fetches and creates English publication from scratch (for initial seeding).
    /// Determines category from publication code and uses discovered languages table.
    /// </summary>
    public async Task<bool> SeedEnglishPublicationAsync(
        string publicationCode,
        CancellationToken cancellationToken = default)
    {
        return await englishContentSeeder.SeedEnglishPublicationAsync(publicationCode, cancellationToken);
    }

    // Methods moved to EnglishContentSeeder helper class

    public async Task<bool> EnsurePublicationExistsAsync(
        string publicationCode,
        string languageCode,
        CancellationToken cancellationToken = default,
        Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null)
    {
        return await publicationEnsurer.EnsurePublicationExistsAsync(publicationCode, languageCode, cancellationToken, progress);
    }

    public async Task<bool> FetchFirstPublicationForLanguageAsync(
        string languageCode,
        string? categoryName = null,
        CancellationToken cancellationToken = default)
    {
        return await publicationEnsurer.FetchFirstPublicationForLanguageAsync(languageCode, categoryName, cancellationToken);
    }

    public async Task<bool> EnsureAllPublicationsForLanguageAsync(
        string languageCode,
        string? categoryName = null,
        CancellationToken cancellationToken = default,
        Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null)
    {
        return await publicationEnsurer.EnsureAllPublicationsForLanguageAsync(languageCode, categoryName, cancellationToken, progress);
    }

    public async Task<bool> EnsureAllSectionsForPublicationAsync(
        string publicationCode,
        string languageCode,
        CancellationToken cancellationToken = default,
        Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null)
    {
        return await publicationEnsurer.EnsureAllSectionsForPublicationAsync(publicationCode, languageCode, cancellationToken, progress);
    }

    public async Task<bool> FetchFirstSectionOnlyAsync(
        string publicationCode,
        string firstSectionCode,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            // For dramas, use case-sensitive publication codes: "Dramas" or "DramaticBibleReadings"
            var lowerCode = publicationCode.ToLowerInvariant();
            var isDrama = PublicationTypeHelper.IsDrama(lowerCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = lowerCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = publicationCode;
            }

            // Get PublicationLanguage to determine category
            var publicationLanguage = await db.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Language)
                .FirstOrDefaultAsync(
                    pl => pl.PublicationCode == publicationCodeForDb &&
                          pl.Language != null &&
                          pl.Language.LanguageCode == normalizedLanguageCode,
                    cancellationToken);

            if (publicationLanguage == null)
            {
                logger.Warning("Language {LanguageCode} is not available for publication {PublicationCode} (not in PublicationLanguages)",
                    languageCode, publicationCode);
                return false;
            }

            // Get English publication as template
            var englishPublication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.Category)
                .Include(bp => bp.Sections)
                .AsSplitQuery()
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.LanguageCode == "E",
                    cancellationToken);

            if (englishPublication == null)
            {
                logger.Warning("English publication {PublicationCode} not found (required as template)", publicationCode);
                return false;
            }

            // Check if publication already exists and if first section exists
            var existingPublication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.Sections)
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.LanguageCode == normalizedLanguageCode,
                    cancellationToken);

            if (existingPublication != null)
            {
                // Check if first section already exists
                var firstSectionExists = existingPublication.Sections
                    .Any(s => s.SectionCode.Equals(firstSectionCode, StringComparison.OrdinalIgnoreCase));
                
                if (firstSectionExists)
                {
                    logger.Debug("Publication {PublicationCode} for language {LanguageCode} already exists with first section",
                        publicationCode, languageCode);
                    return true;
                }
                
                // Publication exists but first section doesn't - we need to add it
                // For now, we'll fetch all sections (this is a rare case)
                // TODO: Optimize to add only the first section to existing publication
                logger.Debug("Publication {PublicationCode} exists but first section doesn't, fetching all sections",
                    publicationCode);
                return await FetchPublicationSectionsAsync(publicationCode, languageCode, cancellationToken);
            }

            // Fetch only the first section - pass only firstSectionCode to section fetcher
            var sectionCodes = new List<string> { firstSectionCode };
            return await sectionFetcher.FetchPublicationSectionsAsync(
                db, publicationCodeForDb, normalizedLanguageCode, publicationCodeForDb,
                englishPublication, sectionCodes, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error fetching first section only for {PublicationCode} in language {LanguageCode}",
                publicationCode, languageCode);
            return false;
        }
    }

    // Method moved to VideoLocalizedNameFetcher helper class
}
