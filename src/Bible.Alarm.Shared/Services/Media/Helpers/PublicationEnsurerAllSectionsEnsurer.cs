#nullable enable

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using IFetchProgress = Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

internal sealed class PublicationEnsurerAllSectionsEnsurer
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;
    private readonly ILanguageContentService languageContentService;

    public PublicationEnsurerAllSectionsEnsurer(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        ILanguageContentService languageContentService)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.languageContentService = languageContentService ?? throw new ArgumentNullException(nameof(languageContentService));
    }

    public async Task<bool> EnsureAllSectionsForPublicationAsync(
        string publicationCode,
        string languageCode,
        IFetchProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            int missingCount;
            using (var scope = scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

                var normalizedPublicationCode = publicationCode.ToLowerInvariant();
                var normalizedLanguageCode = languageCode.ToUpperInvariant();

                var publicationCodeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(normalizedPublicationCode) ?? normalizedPublicationCode;

                var publication = await db.BiblePublications
                    .Include(bp => bp.Sections)
                    .FirstOrDefaultAsync(
                        bp => bp.PublicationCode == publicationCodeForDb &&
                              bp.Language != null &&
                              bp.Language.LanguageCode == normalizedLanguageCode,
                        cancellationToken);

                if (publication == null)
                {
                    logger.Warning("Publication {PublicationCode} not found for language {LanguageCode}",
                        publicationCode, languageCode);
                    return false;
                }

                // Use case-sensitive code for dramas when querying database
                var availableSectionCodes = await db.SectionLanguages
                    .AsNoTracking()
                    .Include(sl => sl.Language)
                    .Where(sl => sl.PublicationCode == publicationCodeForDb &&
                                 sl.Language != null &&
                                 sl.Language.LanguageCode == normalizedLanguageCode)
                    .Select(sl => sl.SectionCode)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var existingSectionCodes = publication.Sections
                    .Where(s => !string.IsNullOrEmpty(s.SectionCode))
                    .Select(s => s.SectionCode!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var missingSectionCodes = availableSectionCodes
                    .Where(sc => !existingSectionCodes.Contains(sc))
                    .ToList();

                missingCount = missingSectionCodes.Count;

                if (missingCount == 0)
                {
                    logger.Debug("All sections already exist for publication {PublicationCode} in language {LanguageCode}",
                        publicationCode, languageCode);
                    return true;
                }
            }

            // Scope disposed here so only one connection is open during fetch (avoids SQLite "database is locked")
            logger.Information("Found {Count} missing sections for publication {PublicationCode} in language {LanguageCode}, fetching...",
                missingCount, publicationCode, languageCode);

            progress?.SetIsVisible(true);
            try
            {
                var result = await languageContentService.FetchPublicationSectionsAsync(publicationCode, languageCode, progress, cancellationToken);
                return result;
            }
            finally
            {
                progress?.SetIsVisible(false);
            }
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (SocketException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error ensuring all sections for publication {PublicationCode} in language {LanguageCode}",
                publicationCode, languageCode);
            return false;
        }
    }
}

