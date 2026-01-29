#nullable enable

using System;
using System.Linq;
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
        CancellationToken cancellationToken = default,
        IFetchProgress? progress = null)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            // For dramas, use case-sensitive publication codes: "Dramas" or "DramaticBibleReadings"
            var isDrama = PublicationTypeHelper.IsDrama(normalizedPublicationCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = normalizedPublicationCode;
            }

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

            // Get all available sections from SectionLanguages
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

            // Get existing section codes
            var existingSectionCodes = publication.Sections
                .Select(s => s.SectionCode.ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Find missing sections
            var missingSectionCodes = availableSectionCodes
                .Where(sc => !existingSectionCodes.Contains(sc.ToLowerInvariant()))
                .ToList();

            if (missingSectionCodes.Count == 0)
            {
                logger.Debug("All sections already exist for publication {PublicationCode} in language {LanguageCode}",
                    publicationCode, languageCode);
                return true;
            }

            logger.Information("Found {Count} missing sections for publication {PublicationCode} in language {LanguageCode}, fetching...",
                missingSectionCodes.Count, publicationCode, languageCode);

            progress?.UpdateProgressText($"Loading sections... (0/{missingSectionCodes.Count})");
            progress?.UpdateProgress(0.0);

            // Fetch each missing section (without tracks - tracks are fetched when section is selected)
            // We need to fetch sections one by one and add them to the existing publication
            // For now, we'll fetch all sections which will replace existing ones
            // TODO: Optimize to fetch only missing sections
            var result = await languageContentService.FetchPublicationSectionsAsync(publicationCode, languageCode, cancellationToken);

            if (result)
            {
                progress?.UpdateProgress(0.5);
                progress?.UpdateProgressText("Loading tracks...");

                // Re-query publication to get all sections (including newly fetched ones)
                publication = await db.BiblePublications
                    .Include(bp => bp.Sections)
                        .ThenInclude(s => s.Tracks)
                    .FirstOrDefaultAsync(
                        bp => bp.PublicationCode == publicationCodeForDb &&
                              bp.Language != null &&
                              bp.Language.LanguageCode == normalizedLanguageCode,
                        cancellationToken);

                if (publication != null && publication.Sections != null)
                {
                    // Check and harvest tracks for each section that doesn't have tracks
                    var sectionsNeedingTracks = publication.Sections
                        .Where(s => s.Tracks == null || s.Tracks.Count == 0)
                        .ToList();

                    if (sectionsNeedingTracks.Count > 0)
                    {
                        logger.Information("Found {Count} sections without tracks for publication {PublicationCode} in language {LanguageCode}, fetching tracks...",
                            sectionsNeedingTracks.Count, publicationCode, languageCode);

                        for (int i = 0; i < sectionsNeedingTracks.Count; i++)
                        {
                            var section = sectionsNeedingTracks[i];
                            var progressPercent = 0.5 + ((double)i / sectionsNeedingTracks.Count) * 0.5;
                            progress?.UpdateProgress(progressPercent);
                            progress?.UpdateProgressText($"Loading tracks for section {i + 1}/{sectionsNeedingTracks.Count}...");

                            // Check again if tracks exist (they might have been fetched by another process)
                            await db.Entry(section).Collection(s => s.Tracks).LoadAsync(cancellationToken);
                            if (section.Tracks == null || section.Tracks.Count == 0)
                            {
                                await languageContentService.FetchSectionTracksAsync(
                                    publicationCode, section.SectionCode, languageCode, cancellationToken);
                            }
                        }
                    }
                }

                progress?.UpdateProgress(1.0);
                progress?.UpdateProgressText("Complete");
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error ensuring all sections for publication {PublicationCode} in language {LanguageCode}",
                publicationCode, languageCode);
            return false;
        }
    }
}

