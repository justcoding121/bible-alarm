#nullable enable

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
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
    private readonly MediatorFetcher mediatorFetcher;
    private readonly FlatPublicationFetcher flatPublicationFetcher;
    private readonly IInternetConnectivityChecker? internetConnectivityChecker;

    public LanguageContentPublicationTracksFetcher(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        MediatorFetcher mediatorFetcher,
        FlatPublicationFetcher flatPublicationFetcher,
        IInternetConnectivityChecker? internetConnectivityChecker = null)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.mediatorFetcher = mediatorFetcher ?? throw new ArgumentNullException(nameof(mediatorFetcher));
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

            var lowerCode = publicationCode.ToLowerInvariant();
            var publicationCodeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(lowerCode) ?? publicationCode;

            // Get PublicationLanguage to determine catalog type and category
            // CatalogType is sufficient to determine if ad-hoc fetching is possible
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
                          bp.Language.LanguageCode == AppConstants.Media.DefaultLanguageCode,
                    cancellationToken);

            if (englishPublication == null)
            {
                logger.Warning("English publication {PublicationCode} not found (required as template for ad-hoc fetching)", publicationCode);
                return false;
            }

            // Use CatalogType from PublicationLanguage to determine fetching method
            var category = publicationLanguage.Category;
            var categoryCode = category.CategoryCode;
            var isVideo = PublicationTypeHelper.IsVideo(lowerCode);

            var catalogType = publicationLanguage.CatalogType ?? PublicationTypeHelper.GetCatalogType(lowerCode);
            await NetworkExceptionHelper.ThrowIfNoInternetAsync(internetConnectivityChecker);

            switch (catalogType)
            {
                case Models.Enums.CatalogType.MediatorSectioned:
                    // Drama publications use Mediator API
                    return await mediatorFetcher.FetchMediatorPublicationTracksAsync(
                        db, publicationCodeForDb, normalizedLanguageCode, englishPublication, cancellationToken);

                case Models.Enums.CatalogType.Flat:
                    var isMusic = categoryCode.Equals(AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase);
                    var fileFormat = isVideo ? "MP4" : "MP3";
                    return await flatPublicationFetcher.FetchFlatPublicationTracksAsync(new FetchFlatPublicationTracksRequest(
                        db, publicationCodeForDb, normalizedLanguageCode, englishPublication,
                        isVideo, isMusic, fileFormat, Language: null, cancellationToken));

                case Models.Enums.CatalogType.Sectioned:
                default:
                    logger.Warning("Publication {PublicationCode} has Sectioned catalog type, use FetchPublicationSectionsAsync instead",
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

