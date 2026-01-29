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

        // Data-driven approach: Get all publications that need English seeding
        // This includes:
        // 1. Publications from discovery phase (dataStore.PublicationLanguages) that haven't been harvested yet
        // 2. Publications that have been harvested but don't have English yet (excluding those with LanguageId == null)

        // Step 1: Get all distinct publication codes from discovery phase
        var discoveredPublicationCodes = dataStore.PublicationLanguages.Keys.ToList();

        // Step 2: Get all distinct publication codes from BiblePublications (excluding those with LanguageId == null)
        // Publications with LanguageId == null don't have English content, so skip them
        var harvestedPublicationCodes = await db.BiblePublications
            .AsNoTracking()
            .Where(bp => bp.LanguageId != null) // Exclude publications without language (they don't have English content)
            .Select(bp => bp.PublicationCode)
            .Distinct()
            .ToListAsync();

        // Step 3: Combine both lists and get unique publication codes
        var allPublicationCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in discoveredPublicationCodes)
        {
            allPublicationCodes.Add(code);
        }

        foreach (var code in harvestedPublicationCodes)
        {
            allPublicationCodes.Add(code);
        }

        // Step 4: Filter out publications that already have English or have LanguageId == null
        var publicationsNeedingEnglish = new List<string>();
        foreach (var publicationCode in allPublicationCodes)
        {
            // Normalize publication code for database queries
            var normalizedCode = publicationCode.ToLowerInvariant();
            var isDrama = PublicationTypeHelper.IsDrama(normalizedCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = normalizedCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = normalizedCode;
            }

            // Check if publication has LanguageId == null (skip these - they don't have English content)
            var hasNullLanguage = await db.BiblePublications
                .AsNoTracking()
                .AnyAsync(bp => bp.PublicationCode == publicationCodeForDb && bp.LanguageId == null);

            if (hasNullLanguage)
            {
                logger.Debug("Skipping publication {PublicationCode} - has LanguageId == null (no English content)", publicationCode);
                continue;
            }

            // Check if English already exists for this publication
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

