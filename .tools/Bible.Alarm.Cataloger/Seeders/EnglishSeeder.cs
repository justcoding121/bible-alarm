#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Cataloger.Seeders;

internal sealed class EnglishSeeder
{
    private readonly ILogger logger;
    private readonly IServiceScopeFactory scopeFactory;

    public EnglishSeeder(ILogger logger, IServiceScopeFactory scopeFactory)
    {
        this.logger = logger;
        this.scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Seeds English (E) for all discovered publications using the shared FetchAndSave* methods.
    /// When publicationFilter is set, only those publications are considered. When failedListPath is set, failed codes are written there for --retry-failed.
    /// </summary>
    public async Task SeedEnglish(IReadOnlySet<string>? publicationFilter = null, string? failedListPath = null)
    {
        using var scope = scopeFactory.CreateScope();
        var languageContentService = scope.ServiceProvider.GetRequiredService<Bible.Alarm.Shared.Services.Media.Interfaces.ILanguageContentService>();

        logger.Information("=== Seeding English (E) for all discovered publications ===");

        using var dbScope = scopeFactory.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var allPublicationCodes = publicationFilter != null
            ? publicationFilter
            : (IEnumerable<string>)JwSourceHelper.AllPublicationCodesForEnglishSeeding;

        // Filter out publications that already have English or have LanguageId == null (e.g. iam)
        var publicationsNeedingEnglish = new List<string>();
        foreach (var publicationCode in allPublicationCodes)
        {
            var normalizedCode = publicationCode.ToLowerInvariant();
            var publicationCodeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(normalizedCode) ?? normalizedCode;

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
                               bp.Language.LanguageCode == AppConstants.Media.DefaultLanguageCode);

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

        var failed = new List<string>();
        foreach (var publicationCode in publicationsNeedingEnglish.OrderBy(pc => pc))
        {
            logger.Information("Seeding English for publication: {PublicationCode}", publicationCode);

            var success = await languageContentService.SeedEnglishPublicationAsync(publicationCode);

            if (success)
            {
                logger.Information("✓ Successfully seeded English for publication {PublicationCode}", publicationCode);
            }
            else
            {
                failed.Add(publicationCode);
                logger.Warning("✗ Failed to seed English for publication {PublicationCode}", publicationCode);
            }
        }

        if (failed.Count > 0 && !string.IsNullOrEmpty(failedListPath))
        {
            var dir = Path.GetDirectoryName(failedListPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllLinesAsync(failedListPath, failed.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            logger.Information("Wrote {Count} failed publication code(s) to {Path} for use with --retry-failed", failed.Count, failedListPath);
        }

        logger.Information("=== English seeding completed ===");
    }
}

