#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
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
    private readonly Func<string, string, IFetchProgress?, CancellationToken, Task<bool>> ensurePublicationExists;

    public PublicationEnsurerAllPublicationsEnsurer(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        Func<string, string, IFetchProgress?, CancellationToken, Task<bool>> ensurePublicationExists)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.ensurePublicationExists = ensurePublicationExists ?? throw new ArgumentNullException(nameof(ensurePublicationExists));
    }

    public async Task<bool> EnsureAllPublicationsForLanguageAsync(
        string languageCode,
        string? categoryName = null,
        IFetchProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            List<string> missingPublications;
            using (var scope = scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

                var normalizedLanguageCode = languageCode.ToUpperInvariant();

                if (normalizedLanguageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
                {
                    logger.Debug("Skipping fetch for English language - already pre-cataloged");
                    return true;
                }

                var availablePublications = await db.PublicationLanguages
                    .AsNoTracking()
                    .Include(pl => pl.Language)
                    .Include(pl => pl.Category)
                    .Where(pl => pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode)
                    .Where(pl => categoryName == null || (pl.Category != null && pl.Category.CategoryCode == categoryName))
                    .Select(pl => pl.PublicationCode)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var existingPublications = await db.BiblePublications
                    .AsNoTracking()
                    .Include(bp => bp.Language)
                    .Where(bp => bp.Language != null && bp.Language.LanguageCode == normalizedLanguageCode)
                    .Select(bp => bp.PublicationCode)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                missingPublications = availablePublications
                    .Where(pub => !existingPublications.Contains(pub))
                    .ToList();
            }

            if (missingPublications.Count == 0)
            {
                logger.Debug("All publications already exist for language {LanguageCode} in category {CategoryName}",
                    languageCode, categoryName ?? "all");
                return true;
            }

            logger.Information("Found {Count} missing publications for language {LanguageCode} in category {CategoryName}, fetching...",
                missingPublications.Count, languageCode, categoryName ?? "all");

            var effectiveToken = progress?.CancellationToken ?? cancellationToken;

            progress?.SetIsVisible(true);
            progress?.UpdateProgress(0.0);

            var successCount = 0;
            var totalCount = missingPublications.Count;
            for (int i = 0; i < totalCount; i++)
            {
                effectiveToken.ThrowIfCancellationRequested();

                var publicationCode = missingPublications[i];

                try
                {
                    var success = await ensurePublicationExists(publicationCode, languageCode, null, effectiveToken);
                    if (success)
                    {
                        successCount++;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }

                var progressPercent = (double)(i + 1) / totalCount;
                progress?.UpdateProgress(progressPercent);
            }

            logger.Information("Successfully fetched {SuccessCount} out of {TotalCount} publications for language {LanguageCode}",
                successCount, missingPublications.Count, languageCode);

            progress?.SetIsVisible(false);

            return successCount > 0;
        }
        catch (OperationCanceledException)
        {
            // Hide progress bar on cancellation
            progress?.SetIsVisible(false);
            throw;
        }
        catch (HttpRequestException)
        {
            progress?.SetIsVisible(false);
            throw;
        }
        catch (SocketException)
        {
            progress?.SetIsVisible(false);
            throw;
        }
        catch (Exception ex)
        {
            // Hide progress bar on error
            progress?.SetIsVisible(false);
            logger.Error(ex, "Error ensuring all publications for language {LanguageCode}",
                languageCode);
            return false;
        }
    }
}

