#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Helper class for ensuring publications exist and fetching first publications for languages.
/// </summary>
internal sealed class PublicationEnsurer
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;
    private readonly ILanguageContentService languageContentService;
    private readonly PublicationEnsurerAllPublicationsEnsurer allPublicationsEnsurer;
    private readonly PublicationEnsurerAllSectionsEnsurer allSectionsEnsurer;

    public PublicationEnsurer(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        ILanguageContentService languageContentService)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.languageContentService = languageContentService ?? throw new ArgumentNullException(nameof(languageContentService));
        allPublicationsEnsurer = new PublicationEnsurerAllPublicationsEnsurer(
            scopeFactory,
            logger,
            (pubCode, langCode, ct, prog) => EnsurePublicationExistsAsync(pubCode, langCode, ct, prog));
        allSectionsEnsurer = new PublicationEnsurerAllSectionsEnsurer(scopeFactory, logger, languageContentService);
    }

    public async Task<bool> EnsurePublicationExistsAsync(
        string publicationCode,
        string languageCode,
        CancellationToken cancellationToken = default,
        Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null)
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

            // Check if publication already exists for this language
            var existingPublication = await db.BiblePublications
                .AsNoTracking()
                .Include(bp => bp.Language)
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.LanguageCode == normalizedLanguageCode,
                    cancellationToken);

            if (existingPublication != null)
            {
                logger.Debug("Publication {PublicationCode} for language {LanguageCode} already exists, skipping fetch",
                    publicationCode, languageCode);
                return true;
            }

            // Publication doesn't exist - check if it's available in PublicationLanguage with a HarvestType
            // Use case-sensitive code for dramas when querying database
            // HarvestType is sufficient to determine if ad-hoc fetching is possible
            var publicationLanguage = await db.PublicationLanguages
                .AsNoTracking()
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

            // Determine harvest type to decide which fetch method to use
            var harvestType = publicationLanguage.HarvestType ?? 
                PublicationTypeHelper.GetHarvestType(lowerCode);

            // Fetch based on harvest type
                if (harvestType == Models.Enums.HarvestType.Sectioned)
                {
                    // Publication has sections - fetch only the first section with tracks (for language selection)
                    // This avoids fetching all sections when user just selects a language
                    progress?.UpdateProgressText($"Loading {publicationCode}...");
                    progress?.UpdateProgress(0.0);
                    var result = await FetchFirstSectionWithTracksAsync(publicationCode, languageCode, cancellationToken);
                    if (result)
                    {
                        progress?.UpdateProgress(1.0);
                        progress?.UpdateProgressText("Complete");
                    }
                    return result;
                }
                else
                {
                    // Publication has flat tracks - fetch tracks
                    progress?.UpdateProgressText($"Loading {publicationCode}...");
                    progress?.UpdateProgress(0.0);
                    var result = await languageContentService.FetchPublicationTracksAsync(publicationCode, languageCode, cancellationToken);
                    if (result)
                    {
                        progress?.UpdateProgress(1.0);
                        progress?.UpdateProgressText("Complete");
                    }
                    return result;
                }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error ensuring publication exists for {PublicationCode} in language {LanguageCode}",
                publicationCode, languageCode);
            return false;
        }
    }

    public async Task<bool> FetchFirstPublicationForLanguageAsync(
        string languageCode,
        string? categoryName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            // Skip English - it's pre-harvested
            if (normalizedLanguageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
            {
                logger.Debug("Skipping fetch for English language - already pre-harvested");
                return true;
            }

            // Get available publications from PublicationLanguage, sorted by priority
            var availablePublications = await db.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Language)
                .Include(pl => pl.Category)
                .Where(pl => pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode)
                .Where(pl => categoryName == null || (pl.Category != null && pl.Category.CategoryName == categoryName))
                .Select(pl => pl.PublicationCode)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (availablePublications.Count == 0)
            {
                logger.Warning("No publications available for language {LanguageCode} in category {CategoryName}",
                    languageCode, categoryName ?? "all");
                return false;
            }

            // Priority: nwt first, then bi12, then others
            var priorityCodes = new[] { "nwt", "bi12" };
            var sortedPublications = availablePublications
                .OrderBy(pub =>
                {
                    var lower = pub.ToLowerInvariant();
                    for (int i = 0; i < priorityCodes.Length; i++)
                    {
                        if (lower == priorityCodes[i])
                            return i;
                    }
                    return priorityCodes.Length;
                })
                .ToList();

            // Try each publication until one succeeds
            foreach (var publicationCode in sortedPublications)
            {
                // publicationCode from PublicationLanguages is already case-sensitive for dramas
                // Use it as-is for database queries
                var normalizedPublicationCode = publicationCode.ToLowerInvariant();
                var isDrama = PublicationTypeHelper.IsDrama(normalizedPublicationCode);
                var publicationCodeForDb = isDrama
                    ? publicationCode // Already case-sensitive from database
                    : normalizedPublicationCode;

                var existing = await db.BiblePublications
                    .AsNoTracking()
                    .Include(bp => bp.Language)
                    .AnyAsync(
                        bp => bp.PublicationCode == publicationCodeForDb &&
                              bp.Language != null &&
                              bp.Language.LanguageCode == normalizedLanguageCode,
                        cancellationToken);

                if (existing)
                {
                    logger.Debug("Publication {PublicationCode} already exists for language {LanguageCode}",
                        publicationCode, languageCode);
                    continue; // Try next publication
                }

                // Get PublicationLanguage to determine harvest type
                // Use case-sensitive code for dramas when querying database
                var publicationLanguage = await db.PublicationLanguages
                    .AsNoTracking()
                    .Include(pl => pl.Language)
                    .Include(pl => pl.Category)
                    .FirstOrDefaultAsync(
                        pl => pl.PublicationCode == publicationCodeForDb &&
                              pl.Language != null &&
                              pl.Language.LanguageCode == normalizedLanguageCode,
                        cancellationToken);

                if (publicationLanguage == null)
                {
                    continue;
                }

                var harvestType = publicationLanguage.HarvestType ??
                    PublicationTypeHelper.GetHarvestType(normalizedPublicationCode);

                bool success = false;
                if (harvestType == Models.Enums.HarvestType.Sectioned)
                {
                    // Fetch first section only with tracks
                    success = await FetchFirstSectionWithTracksAsync(
                        publicationCode, languageCode, cancellationToken);
                }
                else if (harvestType == Models.Enums.HarvestType.Flat)
                {
                    // Fetch all tracks (flat publication)
                    success = await languageContentService.FetchPublicationTracksAsync(
                        publicationCode, languageCode, cancellationToken);
                }
                else if (harvestType == Models.Enums.HarvestType.MediatorSectioned)
                {
                    // Drama - fetch all tracks (they're flat)
                    success = await languageContentService.FetchPublicationTracksAsync(
                        publicationCode, languageCode, cancellationToken);
                }

                if (success)
                {
                    logger.Information("Successfully fetched first publication {PublicationCode} for language {LanguageCode}",
                        publicationCode, languageCode);
                    return true;
                }
            }

            logger.Warning("Failed to fetch any publication for language {LanguageCode}",
                languageCode);
            return false;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error fetching first publication for language {LanguageCode}",
                languageCode);
            return false;
        }
    }

    /// <summary>
    /// Fetches the first section of a publication with its tracks.
    /// Creates the publication if it doesn't exist.
    /// </summary>
    private async Task<bool> FetchFirstSectionWithTracksAsync(
        string publicationCode,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

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

            // Get first section code from SectionLanguages
            // Use case-sensitive code for dramas when querying database
            var firstSectionCode = await db.SectionLanguages
                .AsNoTracking()
                .Include(sl => sl.Language)
                .Where(sl => sl.PublicationCode == publicationCodeForDb &&
                           sl.Language != null &&
                           sl.Language.LanguageCode == normalizedLanguageCode)
                .OrderBy(sl => sl.SectionCode)
                .Select(sl => sl.SectionCode)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrEmpty(firstSectionCode))
            {
                logger.Warning("No sections found for publication {PublicationCode} in language {LanguageCode}",
                    publicationCode, languageCode);
                return false;
            }

            // Check if publication exists (publicationCodeForDb already set above)
            var existingPublication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.Category)
                .Include(bp => bp.Sections)
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.LanguageCode == normalizedLanguageCode,
                    cancellationToken);

            if (existingPublication != null)
            {
                // Publication exists - check if first section exists
                var firstSection = existingPublication.Sections
                    .FirstOrDefault(s => s.SectionCode.Equals(firstSectionCode, StringComparison.OrdinalIgnoreCase));

                if (firstSection != null)
                {
                    // Section exists - check if it has tracks
                    await db.Entry(firstSection).Collection(s => s.Tracks).LoadAsync(cancellationToken);
                    if (firstSection.Tracks != null && firstSection.Tracks.Count > 0)
                    {
                        logger.Debug("First section {SectionCode} already has tracks for publication {PublicationCode}",
                            firstSectionCode, publicationCode);
                        return true;
                    }

                    // Section exists but no tracks - fetch tracks
                    return await languageContentService.FetchSectionTracksAsync(publicationCode, firstSectionCode, languageCode, cancellationToken);
                }
                else
                {
                    // Publication exists but first section doesn't - need to fetch all sections
                    // (FetchPublicationSectionsAsync will replace the publication, which is okay)
                    var sectionsFetched = await languageContentService.FetchPublicationSectionsAsync(publicationCode, languageCode, cancellationToken);
                    if (sectionsFetched)
                    {
                        // Now fetch tracks for first section
                        return await languageContentService.FetchSectionTracksAsync(publicationCode, firstSectionCode, languageCode, cancellationToken);
                    }
                    return false;
                }
            }
            else
            {
                // Publication doesn't exist - fetch only the first section to create publication, then fetch tracks
                // This avoids fetching all sections when we only need the first one
                var firstSectionFetched = await FetchSingleSectionForNewPublicationAsync(
                    publicationCode, firstSectionCode, languageCode, cancellationToken);

                if (firstSectionFetched)
                {
                    // Now fetch tracks for first section
                    return await languageContentService.FetchSectionTracksAsync(publicationCode, firstSectionCode, languageCode, cancellationToken);
                }

                return false;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error fetching first section with tracks for publication {PublicationCode} in language {LanguageCode}",
                publicationCode, languageCode);
            return false;
        }
    }

    public async Task<bool> EnsureAllPublicationsForLanguageAsync(
        string languageCode,
        string? categoryName = null,
        CancellationToken cancellationToken = default,
        Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null)
    {
        return await allPublicationsEnsurer.EnsureAllPublicationsForLanguageAsync(languageCode, categoryName, cancellationToken, progress);
    }

    public async Task<bool> EnsureAllSectionsForPublicationAsync(
        string publicationCode,
        string languageCode,
        CancellationToken cancellationToken = default,
        Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null)
    {
        return await allSectionsEnsurer.EnsureAllSectionsForPublicationAsync(publicationCode, languageCode, cancellationToken, progress);
    }

    /// <summary>
    /// Fetches only the first section when creating a new publication.
    /// This avoids fetching all sections when we only need the first one.
    /// </summary>
    private async Task<bool> FetchSingleSectionForNewPublicationAsync(
        string publicationCode,
        string firstSectionCode,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        // Use the new method that fetches only the first section
        return await languageContentService.FetchFirstSectionOnlyAsync(publicationCode, firstSectionCode, languageCode, cancellationToken);
    }
}
