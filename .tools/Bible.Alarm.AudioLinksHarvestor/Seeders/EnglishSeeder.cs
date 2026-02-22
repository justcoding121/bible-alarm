#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.AudioLinksHarvestor.Utility;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Seeders;

internal sealed class EnglishSeeder
{
    private readonly ILogger logger;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly InMemoryDataStore dataStore;

    public EnglishSeeder(ILogger logger, IServiceScopeFactory scopeFactory, InMemoryDataStore dataStore)
    {
        this.logger = logger;
        this.scopeFactory = scopeFactory;
        this.dataStore = dataStore;
    }

    /// <summary>
    /// Seeds English (E) for all discovered publications using the shared FetchAndSave* methods.
    /// This is the same approach used for other languages in test mode.
    /// </summary>
    public async Task SeedEnglish()
    {
        using var scope = scopeFactory.CreateScope();
        var languageContentService = scope.ServiceProvider.GetRequiredService<Bible.Alarm.Shared.Services.Media.Interfaces.ILanguageContentService>();

        logger.Information("=== Seeding English (E) for all discovered publications ===");

        using var dbScope = scopeFactory.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<MediaDbContext>();

        // Use canonical list from JwSourceHelper so every listed publication (and any future one added there) is harvested for E
        var allPublicationCodes = JwSourceHelper.AllPublicationCodesForEnglishSeeding;

        // Filter out publications that already have English or have LanguageId == null (e.g. iam)
        var publicationsNeedingEnglish = new List<string>();
        foreach (var publicationCode in allPublicationCodes)
        {
            var normalizedCode = publicationCode.ToLowerInvariant();
            var publicationCodeForDb = JwSourceHelper.GetCanonicalDramaPublicationCode(normalizedCode) ?? normalizedCode;

            var hasNullLanguage = await db.BiblePublications
                .AsNoTracking()
                .AnyAsync(bp => bp.PublicationCode == publicationCodeForDb && bp.LanguageId == null);

            if (hasNullLanguage)
            {
                logger.Debug("Skipping publication {PublicationCode} - has LanguageId == null (no English content)", publicationCode);
                continue;
            }

            var hasEnglish = await db.BiblePublications
                .AsNoTracking()
                .Include(bp => bp.Language)
                .AnyAsync(bp => bp.PublicationCode == publicationCodeForDb &&
                               bp.Language != null &&
                               bp.Language.LanguageCode == "E");

            if (!hasEnglish)
            {
                publicationsNeedingEnglish.Add(publicationCode);
            }
        }

        if (publicationsNeedingEnglish.Count == 0)
        {
            logger.Information("All publications already have English seeded or have LanguageId == null, skipping");
            return;
        }

        logger.Information("Found {Count} publication(s) that need English seeding", publicationsNeedingEnglish.Count);

        foreach (var publicationCode in publicationsNeedingEnglish.OrderBy(pc => pc))
        {
            logger.Information("Seeding English for publication: {PublicationCode}", publicationCode);

            // Use shared LanguageContentService to seed English publication
            // This reuses the same code used for ad-hoc fetching
            var success = await languageContentService.SeedEnglishPublicationAsync(publicationCode);

            if (success)
            {
                logger.Information("✓ Successfully seeded English for publication {PublicationCode}", publicationCode);
            }
            else
            {
                logger.Warning("✗ Failed to seed English for publication {PublicationCode}", publicationCode);
            }
        }

        logger.Information("=== English seeding completed ===");
    }
}

