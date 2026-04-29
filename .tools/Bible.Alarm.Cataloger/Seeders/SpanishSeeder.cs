#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Services.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Cataloger.Seeders;

/// <summary>
/// Seeds Spanish (S) for all publications using the same ad-hoc fetch logic as the schedule page.
/// Runs after English seeding. Ensures PublicationLanguages and SectionLanguages have S (caller's responsibility),
/// then calls EnsureAllPublicationsForLanguageAsync and EnsureAllSectionsForPublicationAsync.
/// </summary>
internal sealed class SpanishSeeder
{
    private const string SpanishCode = "S";
    private readonly ILogger logger;
    private readonly IServiceScopeFactory scopeFactory;

    public SpanishSeeder(ILogger logger, IServiceScopeFactory scopeFactory)
    {
        this.logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        this.scopeFactory = scopeFactory ?? throw new System.ArgumentNullException(nameof(scopeFactory));
    }

    /// <summary>
    /// Seeds Spanish using the same API as the app's cascade: EnsureAllPublicationsForLanguageAsync then
    /// EnsureAllSectionsForPublicationAsync for each sectioned publication.
    /// </summary>
    public async Task SeedSpanishAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var httpClient = scope.ServiceProvider.GetRequiredService<System.Net.Http.HttpClient>();
        var languageContentService = new LanguageContentService(scopeFactory, logger, httpClient);

        logger.Information("=== Seeding Spanish (S) for all publications (ad-hoc fetch logic) ===");

        var publicationsFetched = await languageContentService.EnsureAllPublicationsForLanguageAsync(SpanishCode, categoryName: null);
        if (!publicationsFetched)
        {
            logger.Warning("EnsureAllPublicationsForLanguageAsync(S) returned false or no publications fetched");
        }
        else
        {
            logger.Information("Spanish publication fetch completed");
        }

        await SeedAllSectionsForSpanishAsync(scopeFactory, logger, languageContentService);
        logger.Information("=== Spanish (S) seeding completed ===");
    }

    private static async Task SeedAllSectionsForSpanishAsync(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        LanguageContentService languageContentService)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var sectionedPublicationCodes = await db.BiblePublications
            .AsNoTracking()
            .Include(bp => bp.Language)
            .Include(bp => bp.Sections)
            .Where(bp => bp.Language != null
                && bp.Language.LanguageCode == "E"
                && bp.Sections != null
                && bp.Sections.Count > 0)
            .Select(bp => bp.PublicationCode)
            .Distinct()
            .ToListAsync();

        if (sectionedPublicationCodes.Count == 0)
        {
            logger.Debug("No sectioned publications with E found, skipping section fetch for S");
            return;
        }

        logger.Information("Fetching all sections for Spanish for {Count} sectioned publication(s)", sectionedPublicationCodes.Count);

        var successCount = 0;
        var failCount = 0;
        foreach (var publicationCode in sectionedPublicationCodes.OrderBy(pc => pc))
        {
            var success = await languageContentService.EnsureAllSectionsForPublicationAsync(publicationCode, SpanishCode);
            if (success)
            {
                successCount++;
            }
            else
            {
                failCount++;
            }
        }

        logger.Information("Spanish sections: {SuccessCount} succeeded, {FailCount} failed", successCount, failCount);
    }
}
