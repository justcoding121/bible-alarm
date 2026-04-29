#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

internal sealed class LanguageContentPublicationSectionsFetcher
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;
    private readonly SectionFetcher sectionFetcher;
    private readonly IInternetConnectivityChecker? internetConnectivityChecker;

    public LanguageContentPublicationSectionsFetcher(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        SectionFetcher sectionFetcher,
        IInternetConnectivityChecker? internetConnectivityChecker = null)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.sectionFetcher = sectionFetcher ?? throw new ArgumentNullException(nameof(sectionFetcher));
        this.internetConnectivityChecker = internetConnectivityChecker;
    }

    public async Task<bool> FetchPublicationSectionsAsync(
        string publicationCode,
        string languageCode,
        Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            var lowerCode = publicationCode.ToLowerInvariant();
            var publicationCodeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(lowerCode) ?? publicationCode;

            // Get PublicationLanguage to determine catalog type
            // CatalogType is sufficient to determine if ad-hoc fetching is possible
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
                .Include(bp => bp.BiblePublicationCategories)
                .ThenInclude(bp => bp.Category)
                .Include(bp => bp.Sections)
                .AsSplitQuery()
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

            // Get all section codes from SectionLanguages for this publication+language
            // Use case-sensitive code for dramas when querying database
            var sectionCodes = await db.SectionLanguages
                .Include(sl => sl.Language)
                .Where(sl => sl.PublicationCode == publicationCodeForDb &&
                           sl.Language != null &&
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

            await NetworkExceptionHelper.ThrowIfNoInternetAsync(internetConnectivityChecker);

            // DO NOT delete existing publication - SectionFetcher handles incremental section fetching
            // This preserves partial downloads and allows proper resume on retry
            // Progress is reported per-section (divided equally among sections)
            return await sectionFetcher.FetchPublicationSectionsAsync(new FetchPublicationSectionsRequest(
                db,
                publicationCodeForDb,
                normalizedLanguageCode,
                publicationCodeForDb,
                englishPublication,
                sectionCodes,
                cancellationToken,
                progress));
        }
        catch (Exception ex)
        {
            if (NetworkExceptionHelper.IsNetworkFailure(ex))
                throw;
            logger.Error(ex, "Error fetching publication sections for {PublicationCode} in language {LanguageCode}",
                publicationCode, languageCode);
            return false;
        }
    }
}

