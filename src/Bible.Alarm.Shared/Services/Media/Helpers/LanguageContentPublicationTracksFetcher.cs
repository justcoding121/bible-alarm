#nullable enable

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

internal sealed class LanguageContentPublicationTracksFetcher
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;
    private readonly DramaFetcher dramaFetcher;
    private readonly FlatPublicationFetcher flatPublicationFetcher;
    private readonly IInternetConnectivityChecker? internetConnectivityChecker;

    public LanguageContentPublicationTracksFetcher(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        DramaFetcher dramaFetcher,
        FlatPublicationFetcher flatPublicationFetcher,
        IInternetConnectivityChecker? internetConnectivityChecker = null)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.dramaFetcher = dramaFetcher ?? throw new ArgumentNullException(nameof(dramaFetcher));
        this.flatPublicationFetcher = flatPublicationFetcher ?? throw new ArgumentNullException(nameof(flatPublicationFetcher));
        this.internetConnectivityChecker = internetConnectivityChecker;
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
                .Include(bp => bp.BiblePublicationCategories)
                .ThenInclude(bp => bp.Category)
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
            var categoryCode = category.CategoryCode;
            var isVideo = categoryCode.Equals("Dramas", StringComparison.OrdinalIgnoreCase) &&
                         PublicationTypeHelper.IsVideo(lowerCode);

            var harvestType = publicationLanguage.HarvestType ?? PublicationTypeHelper.GetHarvestType(lowerCode);
            await NetworkExceptionHelper.ThrowIfNoInternetAsync(internetConnectivityChecker);

            switch (harvestType)
            {
                case Models.Enums.HarvestType.MediatorSectioned:
                    // Drama publications use Mediator API
                    return await dramaFetcher.FetchDramaPublicationTracksAsync(
                        db, publicationCodeForDb, normalizedLanguageCode, englishPublication, cancellationToken);

                case Models.Enums.HarvestType.Flat:
                    // Music and Video use flat-track fetching
                    var isMusic = categoryCode.Equals("Music", StringComparison.OrdinalIgnoreCase);
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
            if (NetworkExceptionHelper.IsNetworkFailure(ex))
            {
                throw;
            }

            logger.Error(ex, "Error fetching publication tracks for {PublicationCode} in language {LanguageCode}",
                publicationCode, languageCode);
            return false;
        }
    }
}

