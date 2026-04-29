#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

internal sealed class LanguageContentFirstSectionFetcher
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;
    private readonly ILanguageContentService languageContentService;
    private readonly SectionFetcher sectionFetcher;
    private readonly IInternetConnectivityChecker? internetConnectivityChecker;

    public LanguageContentFirstSectionFetcher(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        ILanguageContentService languageContentService,
        SectionFetcher sectionFetcher,
        IInternetConnectivityChecker? internetConnectivityChecker = null)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.languageContentService = languageContentService ?? throw new ArgumentNullException(nameof(languageContentService));
        this.sectionFetcher = sectionFetcher ?? throw new ArgumentNullException(nameof(sectionFetcher));
        this.internetConnectivityChecker = internetConnectivityChecker;
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

            var lowerCode = publicationCode.ToLowerInvariant();
            var publicationCodeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(lowerCode) ?? publicationCode;

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
                .Include(bp => bp.BiblePublicationCategories)
                .ThenInclude(bp => bp.Category)
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
                await NetworkExceptionHelper.ThrowIfNoInternetAsync(internetConnectivityChecker);
                return await languageContentService.FetchPublicationSectionsAsync(
                    publicationCode, languageCode, cancellationToken: cancellationToken);
            }

            await NetworkExceptionHelper.ThrowIfNoInternetAsync(internetConnectivityChecker);

            // Fetch only the first section - pass only firstSectionCode to section fetcher
            var sectionCodes = new List<string> { firstSectionCode };
            return await sectionFetcher.FetchPublicationSectionsAsync(new FetchPublicationSectionsRequest(
                db,
                publicationCodeForDb,
                normalizedLanguageCode,
                publicationCodeForDb,
                englishPublication,
                sectionCodes,
                cancellationToken));
        }
        catch (Exception ex)
        {
            if (NetworkExceptionHelper.IsNetworkFailure(ex))
                throw;
            logger.Error(ex, "Error fetching first section only for {PublicationCode} in language {LanguageCode}",
                publicationCode, languageCode);
            return false;
        }
    }
}

