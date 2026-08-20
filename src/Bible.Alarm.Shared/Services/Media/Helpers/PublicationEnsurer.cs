#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

internal sealed class PublicationEnsurer
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;
    private readonly ILanguageContentService languageContentService;
    private readonly PublicationEnsurerAllPublicationsEnsurer allPublicationsEnsurer;
    private readonly PublicationEnsurerAllSectionsEnsurer allSectionsEnsurer;

    public PublicationEnsurer(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        ILanguageContentService languageContentService)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.languageContentService = languageContentService ?? throw new ArgumentNullException(nameof(languageContentService));
        allPublicationsEnsurer = new PublicationEnsurerAllPublicationsEnsurer(
            scopeFactory,
            logger,
            (pubCode, langCode, prog, ct) => EnsurePublicationExistsAsync(pubCode, langCode, prog, ct));
        allSectionsEnsurer = new PublicationEnsurerAllSectionsEnsurer(scopeFactory, logger, languageContentService);
    }

    private sealed record EnsurePublicationResolution(bool AlreadyHandled, bool Success, Models.Enums.CatalogType? CatalogTypeToFetch);

    private async Task<EnsurePublicationResolution> ResolveEnsurePublicationFetchPlanAsync(
        string publicationCode,
        string languageCode,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var normalizedLanguageCode = languageCode.ToUpperInvariant();

        var lowerCode = publicationCode.ToLowerInvariant();
        var publicationCodeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(lowerCode) ?? publicationCode;

        var existingPublication = await db.BiblePublications
            .AsNoTracking()
            .Include(bp => bp.Language)
            .FirstOrDefaultAsync(
                bp => bp.PublicationCode == publicationCodeForDb &&
                      bp.Language != null &&
                      bp.Language.LanguageCode == normalizedLanguageCode,
                cancellationToken);

        if (existingPublication != null)
        {
            if (existingPublication.CatalogType == null)
            {
                var backfillCatalogType = PublicationTypeHelper.GetCatalogType(lowerCode);
                var toUpdate = await db.BiblePublications
                    .FirstOrDefaultAsync(bp => bp.Id == existingPublication.Id, cancellationToken);
                if (toUpdate != null)
                {
                    toUpdate.CatalogType = backfillCatalogType;
                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            logger.Debug("Publication {PublicationCode} for language {LanguageCode} already exists, skipping fetch",
                publicationCode, languageCode);
            return new EnsurePublicationResolution(AlreadyHandled: true, Success: true, CatalogTypeToFetch: null);
        }

        var publicationLanguage = await db.PublicationLanguages
            .AsNoTracking()
            .Include(pl => pl.Language)
            .Include(pl => pl.Category)
            .FirstOrDefaultAsync(
                pl => pl.PublicationCode == publicationCodeForDb &&
                      pl.Language != null &&
                      pl.Language.LanguageCode == normalizedLanguageCode,
                cancellationToken);

        if (publicationLanguage == null)
        {
            logger.Warning("Language {LanguageCode} is not available for publication {PublicationCode} (not in PublicationLanguages)",
                languageCode, publicationCode);
            return new EnsurePublicationResolution(AlreadyHandled: true, Success: false, CatalogTypeToFetch: null);
        }

        if (publicationLanguage.LanguageId == null)
        {
            logger.Warning("Publication {PublicationCode} doesn't support ad-hoc fetching with language code (has LanguageId = NULL in PublicationLanguages)",
                publicationCode);
            return new EnsurePublicationResolution(AlreadyHandled: true, Success: false, CatalogTypeToFetch: null);
        }

        var catalogTypeToFetch = publicationLanguage.CatalogType ??
            PublicationTypeHelper.GetCatalogType(lowerCode);

        return new EnsurePublicationResolution(AlreadyHandled: false, Success: false, CatalogTypeToFetch: catalogTypeToFetch);
    }

    public async Task<bool> EnsurePublicationExistsAsync(
        string publicationCode,
        string languageCode,
        Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolution = await ResolveEnsurePublicationFetchPlanAsync(publicationCode, languageCode, cancellationToken);
            if (resolution.AlreadyHandled)
            {
                return resolution.Success;
            }

            var catalogTypeToFetch = resolution.CatalogTypeToFetch;

            // Scope disposed so only one connection is open during fetch (avoids SQLite "database is locked")
            if (catalogTypeToFetch == Models.Enums.CatalogType.Sectioned ||
                catalogTypeToFetch == Models.Enums.CatalogType.IssueSectioned)
            {
                progress?.UpdateProgress(0.0);
                var result = await FetchFirstSectionWithTracksAsync(publicationCode, languageCode, progress, cancellationToken);
                if (result)
                {
                    progress?.UpdateProgress(1.0);
                }

                return result;
            }

            progress?.UpdateProgress(0.0);
            var tracksResult = await languageContentService.FetchPublicationTracksAsync(publicationCode, languageCode, cancellationToken);
            if (tracksResult)
            {
                progress?.UpdateProgress(1.0);
            }

            return tracksResult;
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
            logger.Error(ex, "Error ensuring publication exists for {PublicationCode} in language {LanguageCode}",
                publicationCode, languageCode);
            return false;
        }
    }

    private async Task TryAppendUncatalogedPublicationCandidateAsync(
        MediaDbContext db,
        string normalizedLanguageCode,
        string languageCode,
        string publicationCode,
        System.Collections.Generic.List<(string PublicationCode, Models.Enums.CatalogType CatalogType)> candidates,
        CancellationToken cancellationToken)
    {
        var normalizedPublicationCode = publicationCode.ToLowerInvariant();
        var publicationCodeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(normalizedPublicationCode) ?? publicationCode;

        var existing = await db.BiblePublications
            .AsNoTracking()
            .Include(bp => bp.Language)
            .AnyAsync(
                bp => bp.PublicationCode == publicationCodeForDb &&
                      bp.Language != null &&
                      bp.Language.LanguageCode == normalizedLanguageCode,
                cancellationToken);

        if (existing)
        {
            logger.Debug("Publication {PublicationCode} already exists for language {LanguageCode}",
                publicationCode, languageCode);
            return;
        }

        var publicationLanguage = await db.PublicationLanguages
            .AsNoTracking()
            .Include(pl => pl.Language)
            .Include(pl => pl.Category)
            .FirstOrDefaultAsync(
                pl => pl.PublicationCode == publicationCodeForDb &&
                      pl.Language != null &&
                      pl.Language.LanguageCode == normalizedLanguageCode,
                cancellationToken);

        if (publicationLanguage == null)
        {
            return;
        }

        var catalogType = publicationLanguage.CatalogType ??
            PublicationTypeHelper.GetCatalogType(normalizedPublicationCode);
        candidates.Add((publicationCode, catalogType));
    }

    public async Task<bool> FetchFirstPublicationForLanguageAsync(
        string languageCode,
        string? categoryName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            if (normalizedLanguageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                logger.Debug("Skipping fetch for English language - already pre-cataloged");
                return true;
            }

            (string PublicationCode, Models.Enums.CatalogType CatalogType)[] candidates;
            using (var scope = scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

                var availablePublications = await db.PublicationLanguages
                    .AsNoTracking()
                    .Include(pl => pl.Language)
                    .Include(pl => pl.Category)
                    .Where(pl => pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode)
                    .Where(pl => categoryName == null || (pl.Category != null && pl.Category.CategoryCode == categoryName))
                    .Select(pl => pl.PublicationCode)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                if (availablePublications.Count == 0)
                {
                    logger.Warning("No publications available for language {LanguageCode} in category {CategoryName}",
                        languageCode, categoryName ?? "all");
                    return false;
                }

                var sortedPublicationCodes = availablePublications
                    .OrderBy(pub => pub, PublicationCodeHelper.PublicationCodeComparer)
                    .ToList();

                var list = new System.Collections.Generic.List<(string PublicationCode, Models.Enums.CatalogType CatalogType)>();
                foreach (var publicationCode in sortedPublicationCodes)
                {
                    await TryAppendUncatalogedPublicationCandidateAsync(
                        db,
                        normalizedLanguageCode,
                        languageCode,
                        publicationCode,
                        list,
                        cancellationToken);
                }

                candidates = list.ToArray();
            }

            // Scope disposed so only one connection is open during fetch (avoids SQLite "database is locked")
            foreach (var (publicationCode, catalogType) in candidates)
            {
                bool success = false;
                if (catalogType == Models.Enums.CatalogType.Sectioned ||
                    catalogType == Models.Enums.CatalogType.IssueSectioned)
                {
                    success = await FetchFirstSectionWithTracksAsync(
                        publicationCode, languageCode, null, cancellationToken);
                }
                else if (catalogType == Models.Enums.CatalogType.Flat || catalogType == Models.Enums.CatalogType.MediatorSectioned)
                {
                    success = await languageContentService.FetchPublicationTracksAsync(
                        publicationCode, languageCode, cancellationToken);
                }

                if (success)
                {
                    logger.Information("Successfully fetched first publication {PublicationCode} for language {LanguageCode}",
                        publicationCode, languageCode);
                    return true;
                }
            }

            logger.Warning("Failed to fetch any publication for language {LanguageCode}",
                languageCode);
            return false;
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
            logger.Error(ex, "Error fetching first publication for language {LanguageCode}",
                languageCode);
            return false;
        }
    }

    /// <summary>
    /// Fetches the first section of a publication with its tracks.
    /// Creates the publication if it doesn't exist.
    /// Progress milestones: 50% after publication+section saved, 100% after tracks saved.
    /// </summary>
    private enum FirstSectionAction
    {
        FetchSectionTracksOnly,
        FetchAllSectionsThenTracks,
        FetchFirstSectionThenTracks,
    }

    private sealed record FirstSectionProbeResult(bool MissingSections, bool AlreadyComplete, string FirstSectionCode, FirstSectionAction? NextAction);

    private async Task<FirstSectionProbeResult> ProbeFirstSectionStateAsync(
        string publicationCode,
        string languageCode,
        IFetchProgress? progress,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var normalizedPublicationCode = publicationCode.ToLowerInvariant();
        var normalizedLanguageCode = languageCode.ToUpperInvariant();

        var publicationCodeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(normalizedPublicationCode) ?? publicationCode;

        var sectionCodes = await db.SectionLanguages
            .AsNoTracking()
            .Where(sl => sl.PublicationCode == publicationCodeForDb &&
                         sl.Language != null &&
                         sl.Language.LanguageCode == normalizedLanguageCode)
            .Select(sl => sl.SectionCode)
            .ToListAsync(cancellationToken);

        var firstSectionCode = sectionCodes
            .OrderBy(code => code, SectionCodeHelper.SectionCodeComparer)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(firstSectionCode))
        {
            logger.Warning("No sections found for publication {PublicationCode} in language {LanguageCode}",
                publicationCode, languageCode);
            return new FirstSectionProbeResult(MissingSections: true, AlreadyComplete: false, FirstSectionCode: string.Empty, NextAction: null);
        }

        var existingPublication = await db.BiblePublications
            .Include(bp => bp.Language)
            .Include(bp => bp.BiblePublicationCategories)
            .ThenInclude(bp => bp.Category)
            .Include(bp => bp.Sections)
            .FirstOrDefaultAsync(
                bp => bp.PublicationCode == publicationCodeForDb &&
                      bp.Language != null &&
                      bp.Language.LanguageCode == normalizedLanguageCode,
                cancellationToken);

        FirstSectionAction? nextAction = null;
        if (existingPublication != null)
        {
            var firstSection = existingPublication.Sections
                .FirstOrDefault(s => s.SectionCode.Equals(firstSectionCode, StringComparison.OrdinalIgnoreCase));

            if (firstSection != null)
            {
                await db.Entry(firstSection).Collection(s => s.Tracks).LoadAsync(cancellationToken);
                if (firstSection.Tracks != null && firstSection.Tracks.Count > 0)
                {
                    logger.Debug("First section {SectionCode} already has tracks for publication {PublicationCode}",
                        firstSectionCode, publicationCode);
                    progress?.UpdateProgress(1.0);
                    return new FirstSectionProbeResult(MissingSections: false, AlreadyComplete: true, firstSectionCode, NextAction: null);
                }

                nextAction = FirstSectionAction.FetchSectionTracksOnly;
            }
            else
            {
                nextAction = FirstSectionAction.FetchAllSectionsThenTracks;
            }
        }
        else
        {
            nextAction = FirstSectionAction.FetchFirstSectionThenTracks;
        }

        return new FirstSectionProbeResult(MissingSections: false, AlreadyComplete: false, firstSectionCode, nextAction);
    }

    [SuppressMessage("SonarAnalyzer.CSharp", "S2583",
        Justification = "FetchPublicationSectionsAsync / FetchSingleSectionForNewPublicationAsync can return false; Sonar does not fully analyze across ILanguageContentService implementations.")]
    private async Task<bool> FetchFirstSectionWithTracksAsync(
        string publicationCode,
        string languageCode,
        IFetchProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var probe = await ProbeFirstSectionStateAsync(publicationCode, languageCode, progress, cancellationToken);
            if (probe.MissingSections)
            {
                return false;
            }

            if (probe.AlreadyComplete)
            {
                return true;
            }

            var firstSectionCode = probe.FirstSectionCode;
            switch (probe.NextAction!.Value)
            {
                case FirstSectionAction.FetchSectionTracksOnly:
                    progress?.UpdateProgress(0.5);
                    return await languageContentService.FetchSectionTracksAsync(
                        publicationCode, firstSectionCode, languageCode, cancellationToken: cancellationToken);
                case FirstSectionAction.FetchAllSectionsThenTracks:
                {
                    if (!await languageContentService.FetchPublicationSectionsAsync(
                            publicationCode, languageCode, cancellationToken: cancellationToken))
                    {
                        return false;
                    }

                    progress?.UpdateProgress(0.5);
                    return await languageContentService.FetchSectionTracksAsync(
                        publicationCode, firstSectionCode, languageCode, cancellationToken: cancellationToken);
                }
                case FirstSectionAction.FetchFirstSectionThenTracks:
                {
                    if (!await FetchSingleSectionForNewPublicationAsync(
                            publicationCode, firstSectionCode, languageCode, cancellationToken))
                    {
                        return false;
                    }

                    progress?.UpdateProgress(0.5);
                    return await languageContentService.FetchSectionTracksAsync(
                        publicationCode, firstSectionCode, languageCode, cancellationToken: cancellationToken);
                }
                default:
                    throw new InvalidOperationException($"Unexpected FirstSectionAction: {probe.NextAction}");
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
            logger.Error(ex, "Error fetching first section with tracks for publication {PublicationCode} in language {LanguageCode}",
                publicationCode, languageCode);
            return false;
        }
    }

    public async Task<bool> EnsureAllPublicationsForLanguageAsync(
        string languageCode,
        string? categoryName = null,
        Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await allPublicationsEnsurer.EnsureAllPublicationsForLanguageAsync(languageCode, categoryName, progress, cancellationToken);
    }

    public async Task<bool> EnsureAllSectionsForPublicationAsync(
        string publicationCode,
        string languageCode,
        Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await allSectionsEnsurer.EnsureAllSectionsForPublicationAsync(publicationCode, languageCode, progress, cancellationToken);
    }

    /// <summary>
    /// Fetches only the first section when creating a new publication.
    /// This avoids fetching all sections when we only need the first one.
    /// </summary>
    private async Task<bool> FetchSingleSectionForNewPublicationAsync(
        string publicationCode,
        string firstSectionCode,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        return await languageContentService.FetchFirstSectionOnlyAsync(publicationCode, firstSectionCode, languageCode, cancellationToken);
    }
}
