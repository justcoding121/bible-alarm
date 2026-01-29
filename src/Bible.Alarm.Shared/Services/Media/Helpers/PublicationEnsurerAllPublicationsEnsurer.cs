#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using IFetchProgress = Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

internal sealed class PublicationEnsurerAllPublicationsEnsurer
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;
    private readonly Func<string, string, CancellationToken, IFetchProgress?, Task<bool>> ensurePublicationExists;

    public PublicationEnsurerAllPublicationsEnsurer(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        Func<string, string, CancellationToken, IFetchProgress?, Task<bool>> ensurePublicationExists)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.ensurePublicationExists = ensurePublicationExists ?? throw new ArgumentNullException(nameof(ensurePublicationExists));
    }

    public async Task<bool> EnsureAllPublicationsForLanguageAsync(
        string languageCode,
        string? categoryName = null,
        CancellationToken cancellationToken = default,
        IFetchProgress? progress = null)
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

            // Get all available publications from PublicationLanguage
            var availablePublications = await db.PublicationLanguages
                .AsNoTracking()
                .Include(pl => pl.Language)
                .Include(pl => pl.Category)
                .Where(pl => pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode)
                .Where(pl => categoryName == null || (pl.Category != null && pl.Category.CategoryName == categoryName))
                .Select(pl => pl.PublicationCode)
                .Distinct()
                .ToListAsync(cancellationToken);

            // Get existing publications (use case-sensitive codes as stored in database)
            var existingPublications = await db.BiblePublications
                .AsNoTracking()
                .Include(bp => bp.Language)
                .Where(bp => bp.Language != null && bp.Language.LanguageCode == normalizedLanguageCode)
                .Select(bp => bp.PublicationCode)
                .Distinct()
                .ToListAsync(cancellationToken);

            // Find missing publications
            var missingPublications = availablePublications
                .Where(pub => !existingPublications.Contains(pub))
                .ToList();

            if (missingPublications.Count == 0)
            {
                logger.Debug("All publications already exist for language {LanguageCode} in category {CategoryName}",
                    languageCode, categoryName ?? "all");
                return true;
            }

            logger.Information("Found {Count} missing publications for language {LanguageCode} in category {CategoryName}, fetching...",
                missingPublications.Count, languageCode, categoryName ?? "all");

            // Fetch each missing publication with progress updates
            var successCount = 0;
            var totalCount = missingPublications.Count;
            for (int i = 0; i < totalCount; i++)
            {
                var publicationCode = missingPublications[i];
                var progressPercent = (double)i / totalCount;

                progress?.UpdateProgressText($"Loading {publicationCode}... ({i + 1}/{totalCount})");
                progress?.UpdateProgress(progressPercent);

                var success = await ensurePublicationExists(publicationCode, languageCode, cancellationToken, progress);
                if (success)
                {
                    successCount++;
                }
            }

            progress?.UpdateProgress(1.0);
            progress?.UpdateProgressText("Complete");

            logger.Information("Successfully fetched {SuccessCount} out of {TotalCount} publications for language {LanguageCode}",
                successCount, missingPublications.Count, languageCode);

            return successCount > 0;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error ensuring all publications for language {LanguageCode}",
                languageCode);
            return false;
        }
    }
}

