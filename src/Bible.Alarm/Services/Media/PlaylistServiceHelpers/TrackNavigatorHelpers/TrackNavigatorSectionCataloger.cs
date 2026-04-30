#nullable enable
using System.Linq;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers.SectionFetcherHelpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers.TrackNavigatorHelpers;

/// <summary>
/// Handles section cataloging for TrackNavigator when navigating to adjacent sections.
/// </summary>
public sealed class TrackNavigatorSectionCataloger
{
    private readonly IMediaService mediaService;
    private readonly ILanguageContentService? languageContentService;
    private readonly IServiceScopeFactory? scopeFactory;
    private readonly ILogger? logger;
    private readonly Func<string, string, Task<SortedDictionary<string, BiblePublicationSection>>> getSectionsCachedAsync;

    public TrackNavigatorSectionCataloger(
        IMediaService mediaService,
        ILanguageContentService? languageContentService,
        IServiceScopeFactory? scopeFactory,
        ILogger? logger,
        Func<string, string, Task<SortedDictionary<string, BiblePublicationSection>>> getSectionsCachedAsync)
    {
        this.mediaService = mediaService;
        this.languageContentService = languageContentService;
        this.scopeFactory = scopeFactory;
        this.logger = logger;
        this.getSectionsCachedAsync = getSectionsCachedAsync;
    }

    public async Task<List<string>> GetDiscoveredSectionCodesAsync(string languageCode, string publicationCode)
    {
        var isNoLanguagePublication = await IsPublicationWithoutLanguageAsync(publicationCode);
        if (isNoLanguagePublication)
        {
            var sections = await getSectionsCachedAsync(languageCode, publicationCode);
            return sections.Keys.ToList();
        }

        if (scopeFactory == null)
        {
            var sections = await getSectionsCachedAsync(languageCode, publicationCode);
            return sections.Keys.ToList();
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedLanguageCode = languageCode.ToUpperInvariant();
            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            var isDrama = PublicationTypeHelper.IsDrama(normalizedPublicationCode);
            var publicationCodeForDb = GetPublicationCodeForDb(normalizedPublicationCode, isDrama);

            var sectionCodes = await db.SectionLanguages
                .AsNoTracking()
                .Include(sl => sl.Language)
                .Where(sl => sl.PublicationCode == publicationCodeForDb &&
                           sl.Language != null &&
                           sl.Language.LanguageCode == normalizedLanguageCode)
                .Select(sl => sl.SectionCode)
                .Distinct()
                .ToListAsync();

            return sectionCodes;
        }
        catch (Exception ex)
        {
            logger?.Warning(ex, "Failed to get discovered section codes: languageCode={LanguageCode}, publicationCode={PublicationCode}",
                languageCode, publicationCode);
            var sections = await getSectionsCachedAsync(languageCode, publicationCode);
            return sections.Keys.ToList();
        }
    }

    public async Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode)
    {
        if (scopeFactory == null)
        {
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            var publicationCodeForDb = GetPublicationCodeForDb(publicationCode.ToLowerInvariant(),
                PublicationTypeHelper.IsDrama(publicationCode.ToLowerInvariant()));

            var isNoLanguage = await db.BiblePublications
                .AsNoTracking()
                .AnyAsync(bp => bp.PublicationCode == publicationCodeForDb && bp.LanguageId == null);

            if (isNoLanguage)
            {
                return true;
            }

            return await db.PublicationLanguages
                .AsNoTracking()
                .AnyAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == null);
        }
        catch (Exception ex)
        {
            logger?.Warning(ex, "Failed to check if publication is no-language: publicationCode={PublicationCode}", publicationCode);
            return false;
        }
    }

    public async Task<bool> EnsureSectionCatalogedAsync(
        string languageCode,
        string publicationCode,
        string sectionCode,
        IFetchProgress? sectionFetchProgress,
        Action clearSectionsCache,
        Action clearTracksCache)
    {
        var isNoLanguagePublication = await IsPublicationWithoutLanguageAsync(publicationCode);
        if (isNoLanguagePublication)
        {
            logger?.Debug("Publication {PublicationCode} is a no-language publication, cannot ad-hoc catalog sections.", publicationCode);
            var noLanguageSections = await getSectionsCachedAsync(languageCode, publicationCode);
            return noLanguageSections.ContainsKey(sectionCode);
        }

        if (languageContentService == null || scopeFactory == null)
        {
            logger?.Warning("ILanguageContentService or IServiceScopeFactory not available");
            return false;
        }

        var discoveredSectionCodes = await GetDiscoveredSectionCodesAsync(languageCode, publicationCode);
        if (!discoveredSectionCodes.Any(sc => string.Equals(sc, sectionCode, StringComparison.OrdinalIgnoreCase)))
        {
            logger?.Warning("Section {SectionCode} not found in discovered sections", sectionCode);
            return false;
        }

        var sections = await getSectionsCachedAsync(languageCode, publicationCode);
        if (sections.ContainsKey(sectionCode))
        {
            var tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, sectionCode);
            if (tracks.Count > 0)
            {
                return true;
            }
        }

        var networkStatusService = ServiceProviderManager.GetService<INetworkStatusService>();
        if (networkStatusService != null && !await networkStatusService.IsInternetAvailable())
        {
            logger?.Warning("No internet - cannot catalog section for navigation");
            return false;
        }

        try
        {
            logger?.Information("Cataloging section for navigation: languageCode={LanguageCode}, publicationCode={PublicationCode}, sectionCode={SectionCode}",
                languageCode, publicationCode, sectionCode);

            sectionFetchProgress?.UpdateProgress(0.0);

            var publicationProgress = sectionFetchProgress != null
                ? new ScaledFetchProgressAdapter(sectionFetchProgress, 0.5)
                : null;

            // For sectioned (Bible/iam): ensures pub + first section; we then fetch this section's tracks via GETPUBMEDIALINKS (section-level, no track=).
            // For mediator (drama): ensures pub by fetching all tracks from mediator, so sections already have tracks; FetchSectionTracksAsync below is then a no-op.
            var publicationExists = await languageContentService.EnsurePublicationExistsAsync(
                publicationCode,
                languageCode,
                publicationProgress,
                CancellationToken.None);

            if (!publicationExists)
            {
                logger?.Warning("Failed to ensure publication exists");
                return false;
            }

            sectionFetchProgress?.UpdateProgress(0.5);

            using (var scope = scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

                var normalizedPublicationCode = publicationCode.ToLowerInvariant();
                var normalizedSectionCode = sectionCode.ToLowerInvariant();
                var normalizedLanguageCode = languageCode.ToUpperInvariant();
                var publicationCodeForDb = GetPublicationCodeForDb(normalizedPublicationCode,
                    PublicationTypeHelper.IsDrama(normalizedPublicationCode));

                var publication = await db.BiblePublications
                    .Include(bp => bp.Sections)
                    .FirstOrDefaultAsync(
                        bp => bp.PublicationCode == publicationCodeForDb &&
                              bp.Language != null &&
                              bp.Language.LanguageCode == normalizedLanguageCode);

                if (publication == null)
                {
                    logger?.Warning("Publication not found after EnsurePublicationExistsAsync");
                    return false;
                }

                var section = publication.Sections.FirstOrDefault(s =>
                    s.SectionCode.Equals(normalizedSectionCode, StringComparison.OrdinalIgnoreCase));

                if (section == null)
                {
                    logger?.Information("Section entity doesn't exist, creating it");
                    section = new BiblePublicationSection
                    {
                        Name = sectionCode,
                        SectionCode = normalizedSectionCode,
                        BiblePublication = publication,
                        BiblePublicationId = publication.Id,
                        Tracks = new List<BiblePublicationTrack>()
                    };
                    publication.Sections.Add(section);
                    await SaveWithRetryAsync(db, sectionCode);
                }
            }

            // Scope disposed so only one connection is open during fetch (avoids SQLite "database is locked")
            var success = await languageContentService.FetchSectionTracksAsync(
                publicationCode,
                sectionCode,
                languageCode,
                cancellationToken: CancellationToken.None);

            sectionFetchProgress?.UpdateProgress(1.0);

            if (success)
            {
                clearSectionsCache();
                clearTracksCache();
                return true;
            }

            logger?.Warning("Failed to catalog section");
            return false;
        }
        catch (Exception ex)
        {
            logger?.Error(ex, "Error cataloging section");
            return false;
        }
    }

    private async Task SaveWithRetryAsync(MediaDbContext db, string context)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await db.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException ex) when (attempt < maxAttempts && SectionFetcherSqliteExceptionHelper.IsBusyOrLocked(ex))
            {
                logger?.Debug(ex, "SaveChanges locked (attempt {Attempt}/{Max}) for {Context}, retrying",
                    attempt, maxAttempts, context);
                await Task.Delay(100 * attempt);
            }
        }
    }

    private static string GetPublicationCodeForDb(string normalizedPublicationCode, bool isDrama)
    {
        if (isDrama)
        {
            return normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                ? AppConstants.Media.BiblePublicationCategoryDramas
                : AppConstants.Media.BiblePublicationCodeDramaticBibleReadings;
        }
        return normalizedPublicationCode;
    }
}
