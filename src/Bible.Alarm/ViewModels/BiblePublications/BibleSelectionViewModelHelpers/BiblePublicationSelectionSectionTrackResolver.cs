#nullable enable

using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

internal sealed class BiblePublicationSelectionSectionTrackResolver
{
    private readonly IMediaService mediaService;
    private readonly IBiblePublicationService? biblePublicationService;
    private readonly ILanguageContentService? languageContentService;
    private readonly IServiceScopeFactory scopeFactory;

    public BiblePublicationSelectionSectionTrackResolver(
        IMediaService mediaService,
        IServiceScopeFactory scopeFactory,
        IBiblePublicationService? biblePublicationService,
        ILanguageContentService? languageContentService)
    {
        this.mediaService = mediaService;
        this.scopeFactory = scopeFactory;
        this.biblePublicationService = biblePublicationService;
        this.languageContentService = languageContentService;
    }

    internal async Task<(string? SectionCode, string TrackCode, string SectionName, string TrackTitle)>
        GetFirstSectionAndTrackFromSectionsAsync(
            string? languageCode,
            string publicationCode,
            SortedDictionary<string, BiblePublicationSection> sections,
            IFetchProgress? progress = null)
    {
        using var sectionEnumerator = sections.GetEnumerator();
        _ = sectionEnumerator.MoveNext();
        var firstSectionKvp = sectionEnumerator.Current;
        var firstSection = firstSectionKvp.Value;
        var firstSectionCode = firstSectionKvp.Key;
        Log.Debug(AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.GetFirstSectionFirstSectionCodeKey,
            firstSectionCode, firstSection.SectionCode, firstSection.Name);

        SortedDictionary<string, BiblePublicationTrack>? tracks;

        // Publications without LanguageId use null/empty languageCode
        if (string.IsNullOrEmpty(languageCode))
        {
            // Query tracks directly from database for publications without LanguageId
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var pub = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Sections)
                    .ThenInclude(s => s.Tracks)
                .Where(x => x.PublicationCode == publicationCode && x.LanguageId == null)
                .FirstOrDefaultAsync();

            if (pub?.Sections != null)
            {
                var section = pub.Sections.FirstOrDefault(s => SectionCodeHelper.CodeEquals(s.SectionCode, firstSection.SectionCode));
                if (section?.Tracks != null && section.Tracks.Count > 0)
                {
                    var tracksDict = section.Tracks
                        .OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b)))
                        .ToDictionary(t => t.TrackCode, t => t, StringComparer.Ordinal);
                    tracks = new SortedDictionary<string, BiblePublicationTrack>(tracksDict, TrackCodeComparer.Comparer);
                }
                else
                {
                    tracks = new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer);
                }
            }
            else
            {
                tracks = new SortedDictionary<string, BiblePublicationTrack>(TrackCodeComparer.Comparer);
            }
        }
        else
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var pub = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Language)
                .Include(x => x.Sections)
                    .ThenInclude(s => s.Tracks)
                .Where(x => x.PublicationCode == publicationCode &&
                           x.Language != null &&
                           x.Language.LanguageCode == languageCode.ToUpperInvariant())
                .FirstOrDefaultAsync();

            SortedDictionary<string, BiblePublicationTrack>? foundTracks = null;

            if (pub?.Sections != null)
            {
                // Use the actual SectionCode from the section object for matching (case-insensitive)
                var section = pub.Sections.FirstOrDefault(s =>
                    s.SectionCode.Equals(firstSection.SectionCode, StringComparison.OrdinalIgnoreCase));
                if (section?.Tracks != null && section.Tracks.Count > 0)
                {
                    Log.Debug(
                        AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.FoundTracksForSectionUsingDirectQuery,
                        section.Tracks.Count,
                        section.SectionCode);
                    var tracksDict = section.Tracks
                        .OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b)))
                        .ToDictionary(t => t.TrackCode, t => t, StringComparer.Ordinal);
                    foundTracks = new SortedDictionary<string, BiblePublicationTrack>(tracksDict, TrackCodeComparer.Comparer);
                }
                else
                {
                    Log.Warning(
                        AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.SectionFoundButNoTracks,
                        firstSection.SectionCode,
                        publicationCode,
                        languageCode);
                }
            }
            else
            {
                Log.Warning(
                    AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.PublicationNotFoundOrHasNoSections,
                    publicationCode,
                    languageCode);
            }

            tracks = foundTracks;

            // If no tracks found in database, explicitly fetch them (matching cascade behavior)
            // Tracks are NOT automatically fetched when sections are cataloged
            if (tracks == null || tracks.Count == 0)
            {
                Log.Information(AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.NoTracksInDatabaseFetchingFirstSection);
                progress?.UpdateProgress(0.7);
                if (languageContentService != null)
                {
                    var fetchSuccess = await languageContentService.FetchSectionTracksAsync(
                        publicationCode,
                        firstSection.SectionCode,
                        languageCode);

                    if (fetchSuccess)
                    {
                        // Re-query tracks after fetching
                        Log.Debug(AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.TracksFetchedSuccessfullyRequeryingFromDatabase);
                        tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, firstSection.SectionCode);
                    }
                    else
                    {
                        Log.Warning(
                            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.FailedToFetchTracksForSection,
                            firstSection.SectionCode,
                            publicationCode,
                            languageCode);
                    }
                }
            }

            if (tracks == null || tracks.Count == 0)
            {
                Log.Debug(AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.FallingBackToMediaServiceGetBiblePublicationTracks);
                tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, firstSection.SectionCode);
            }
        }

        if (tracks == null || tracks.Count == 0)
        {
            Log.Warning(AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.NoTracksFoundForLanguagePublicationSection,
                languageCode ?? "(null)", publicationCode, firstSection.SectionCode);
            progress?.UpdateProgress(1.0);
            return (null, string.Empty, string.Empty, string.Empty);
        }

        using var trackEnumerator = tracks.Values.GetEnumerator();
        _ = trackEnumerator.MoveNext();
        var firstTrack = trackEnumerator.Current;
        var trackCode = Bible.Alarm.Shared.Helpers.TrackCodeHelper.GetFromTrack(firstTrack);
        Log.Debug(AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.GetFirstSectionFirstTrackCodeAndTitle,
            trackCode, firstTrack.Title);

        progress?.UpdateProgress(1.0);
        return (firstSection.SectionCode, trackCode, firstSection.Name, firstTrack.Title ?? string.Empty);
    }

    internal async Task<(string? SectionCode, string TrackCode, string SectionName, string TrackTitle)>
        GetFirstTrackForNonSectionedAsync(string? languageCode, string publicationCode, IFetchProgress? progress = null)
    {
        Log.Debug(
            AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.GetFirstTrackNonSectionedStarting,
            languageCode,
            publicationCode,
            biblePublicationService != null);

        if (biblePublicationService == null)
        {
            Log.Warning(AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.BiblePublicationServiceNullReturningEmpty);
            return (null, string.Empty, string.Empty, string.Empty);
        }

        BiblePublication? publication;

        // Publications without LanguageId use null/empty languageCode
        if (string.IsNullOrEmpty(languageCode))
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            publication = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.BiblePublicationCategories)
                .ThenInclude(bpc => bpc.Category)
                .Include(x => x.Tracks.Where(t => t.BiblePublicationSectionId == null))
                .Where(x => x.PublicationCode == publicationCode && x.LanguageId == null)
                .FirstOrDefaultAsync();
        }
        else
        {
            publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode);
        }

        Log.Debug(AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.LoadedPublicationNameAndTracksCount,
            publication?.Name ?? "(null)", publication?.Tracks?.Count ?? 0);

        // If no tracks found, ensure publication is cataloged (for non-sectioned publications, this fetches tracks)
        if (publication == null || publication.Tracks == null || publication.Tracks.Count == 0)
        {
            Log.Information(AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.NoTracksEnsuringPublicationExistsFetchNonSectioned);

            if (!string.IsNullOrEmpty(languageCode) && languageContentService != null)
            {
                progress?.UpdateProgress(0.6);
                var catalogSuccess = await languageContentService.EnsurePublicationExistsAsync(publicationCode, languageCode, progress);

                if (catalogSuccess)
                {
                    Log.Debug(AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.PublicationCatalogedSuccessfullyRequeryingTracks);
                    progress?.UpdateProgress(0.8);
                    publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode);
                }
                else
                {
                    Log.Warning(AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.FailedToCatalogPublicationForLanguage,
                        publicationCode, languageCode);
                }
            }
            else if (string.IsNullOrEmpty(languageCode))
            {
                // For publications without language (like "iam"), tracks should already be pre-cataloged
                Log.Warning(AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.NoTracksFoundPublicationWithoutLanguage,
                    publicationCode);
            }
        }

        if (publication == null || publication.Tracks == null || publication.Tracks.Count == 0)
        {
            Log.Warning(AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.NoTracksFoundForLanguageAndPublication,
                languageCode ?? "(null)", publicationCode);
            return (null, string.Empty, string.Empty, string.Empty);
        }

        var firstTrack = publication.Tracks.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList()[0];
        var trackCode = Bible.Alarm.Shared.Helpers.TrackCodeHelper.GetFromTrack(firstTrack);
        Log.Information(AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.FoundFirstTrackCodeAndTitle,
            trackCode, firstTrack.Title);

        progress?.UpdateProgress(1.0);
        return (null, trackCode, string.Empty, firstTrack.Title ?? string.Empty);
    }

    internal async Task<bool> CheckIfPublicationWithFirstSectionCatalogedAsync(string publicationCode, string languageCode)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            // For dramas, use case-sensitive publication codes
            var lowerCode = publicationCode.ToLowerInvariant();
            var isDrama = PublicationTypeHelper.IsDrama(lowerCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = lowerCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? AppConstants.Media.BiblePublicationCategoryDramas
                    : AppConstants.Media.BiblePublicationCodeDramaticBibleReadings;
            }
            else
            {
                publicationCodeForDb = publicationCode;
            }

            // Check if publication exists
            var publication = await db.BiblePublications
                .AsNoTracking()
                .Include(bp => bp.Language)
                .Include(bp => bp.Sections)
                    .ThenInclude(s => s.Tracks)
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.LanguageCode == normalizedLanguageCode);

            if (publication == null)
            {
                return false;
            }

            // Get first section code from SectionLanguages
            var firstSectionCode = await db.SectionLanguages
                .AsNoTracking()
                .Include(sl => sl.Language)
                .Where(sl => sl.PublicationCode == publicationCodeForDb &&
                           sl.Language != null &&
                           sl.Language.LanguageCode == normalizedLanguageCode)
                .OrderBy(sl => sl.SectionCode, SectionCodeHelper.SectionCodeComparer)
                .Select(sl => sl.SectionCode)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(firstSectionCode))
            {
                // No sections defined - check if it's a flat publication (has tracks directly)
                var hasTracks = await db.BiblePublicationTracks
                    .AsNoTracking()
                    .Include(t => t.Publication)
                        .ThenInclude(bp => bp!.Language)
                    .AnyAsync(t => t.Publication != null &&
                                   t.Publication.PublicationCode == publicationCodeForDb &&
                                   t.Publication.Language != null &&
                                   t.Publication.Language.LanguageCode == normalizedLanguageCode &&
                                   t.BiblePublicationSectionId == null);
                return hasTracks;
            }

            // Check if first section exists with tracks
            var firstSection = publication.Sections
                .FirstOrDefault(s => s.SectionCode.Equals(firstSectionCode, StringComparison.OrdinalIgnoreCase));

            if (firstSection == null)
            {
                return false;
            }

            // Check if section has tracks
            return firstSection.Tracks != null && firstSection.Tracks.Count > 0;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, AppConstants.Logging.BiblePublicationSelectionSectionTrackResolverDiagnosticsLog.ErrorCheckingPublicationCataloged,
                publicationCode);
            return false;
        }
    }
}

