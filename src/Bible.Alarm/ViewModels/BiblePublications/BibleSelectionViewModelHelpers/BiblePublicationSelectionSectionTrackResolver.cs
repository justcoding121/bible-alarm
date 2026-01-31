#nullable enable

using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
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

    internal async Task<(int SectionNumber, int TrackNumber, string SectionName, string TrackTitle)>
        GetFirstSectionAndTrackFromSectionsAsync(
            string? languageCode,
            string publicationCode,
            SortedDictionary<int, BiblePublicationSection> sections,
            IFetchProgress? progress = null)
    {
        var firstSectionKvp = sections.First();
        var firstSection = firstSectionKvp.Value;
        var firstSectionNumber = firstSectionKvp.Key; // Use the dictionary key (parsed from SectionCode)
        Log.Debug("GetFirstSectionAndTrackFromSectionsAsync: First section number={SectionNumber}, name={SectionName}",
            firstSectionNumber, firstSection.Name);

        SortedDictionary<int, BiblePublicationTrack>? tracks;

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
                var section = pub.Sections.FirstOrDefault(s => s.SectionCode == firstSection.SectionCode);
                if (section?.Tracks != null && section.Tracks.Count > 0)
                {
                    var tracksDict = section.Tracks
                        .OrderBy(t => t.Number)
                        .ToDictionary(t => t.Number, t => t);
                    tracks = new SortedDictionary<int, BiblePublicationTrack>(tracksDict);
                }
                else
                {
                    tracks = new SortedDictionary<int, BiblePublicationTrack>();
                }
            }
            else
            {
                tracks = new SortedDictionary<int, BiblePublicationTrack>();
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

            SortedDictionary<int, BiblePublicationTrack>? foundTracks = null;

            if (pub?.Sections != null)
            {
                // Use the actual SectionCode from the section object for matching (case-insensitive)
                var section = pub.Sections.FirstOrDefault(s =>
                    s.SectionCode.Equals(firstSection.SectionCode, StringComparison.OrdinalIgnoreCase));
                if (section?.Tracks != null && section.Tracks.Count > 0)
                {
                    Log.Debug(
                        "GetFirstSectionAndTrackFromSectionsAsync: Found {TrackCount} tracks for section={SectionCode} using direct query",
                        section.Tracks.Count,
                        section.SectionCode);
                    var tracksDict = section.Tracks
                        .OrderBy(t => t.Number)
                        .ToDictionary(t => t.Number, t => t);
                    foundTracks = new SortedDictionary<int, BiblePublicationTrack>(tracksDict);
                }
                else
                {
                    Log.Warning(
                        "GetFirstSectionAndTrackFromSectionsAsync: Section found but no tracks. SectionCode={SectionCode}, PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                        firstSection.SectionCode,
                        publicationCode,
                        languageCode);
                }
            }
            else
            {
                Log.Warning(
                    "GetFirstSectionAndTrackFromSectionsAsync: Publication not found or has no sections. PublicationCode={PublicationCode}, LanguageCode={LanguageCode}",
                    publicationCode,
                    languageCode);
            }

            tracks = foundTracks;

            // If no tracks found in database, explicitly fetch them (matching cascade behavior)
            // Tracks are NOT automatically fetched when sections are harvested
            if (tracks == null || tracks.Count == 0)
            {
                Log.Information("GetFirstSectionAndTrackFromSectionsAsync: No tracks found in database, fetching tracks for first section...");
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
                        Log.Debug("GetFirstSectionAndTrackFromSectionsAsync: Tracks fetched successfully, re-querying from database");
                        tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, firstSectionNumber);
                    }
                    else
                    {
                        Log.Warning(
                            "GetFirstSectionAndTrackFromSectionsAsync: Failed to fetch tracks for section={SectionCode}, publication={PublicationCode}, language={LanguageCode}",
                            firstSection.SectionCode,
                            publicationCode,
                            languageCode);
                    }
                }
            }

            if (tracks == null || tracks.Count == 0)
            {
                Log.Debug("GetFirstSectionAndTrackFromSectionsAsync: Falling back to mediaService.GetBiblePublicationTracks");
                tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, firstSectionNumber);
            }
        }

        if (tracks == null || tracks.Count == 0)
        {
            Log.Warning("GetFirstSectionAndTrackFromSectionsAsync: No tracks found for language={LanguageCode}, publication={PublicationCode}, section={SectionNumber}",
                languageCode ?? "(null)", publicationCode, firstSectionNumber);
            progress?.UpdateProgress(1.0);
            return (0, 0, string.Empty, string.Empty);
        }

        var firstTrack = tracks.Values.First();
        Log.Debug("GetFirstSectionAndTrackFromSectionsAsync: First track number={TrackNumber}, title={TrackTitle}",
            firstTrack.Number, firstTrack.Title);

        progress?.UpdateProgress(1.0);
        return (firstSectionNumber, firstTrack.Number, firstSection.Name, firstTrack.Title);
    }

    internal async Task<(int SectionNumber, int TrackNumber, string SectionName, string TrackTitle)>
        GetFirstTrackForNonSectionedAsync(string? languageCode, string publicationCode, IFetchProgress? progress = null)
    {
        Log.Debug(
            "GetFirstTrackForNonSectionedAsync: Starting for language={LanguageCode}, publication={PublicationCode}, biblePublicationService={HasService}",
            languageCode,
            publicationCode,
            biblePublicationService != null);

        if (biblePublicationService == null)
        {
            Log.Warning("GetFirstTrackForNonSectionedAsync: biblePublicationService is null, returning empty result");
            return (0, 0, string.Empty, string.Empty);
        }

        BiblePublication? publication;

        // Publications without LanguageId use null/empty languageCode
        if (string.IsNullOrEmpty(languageCode))
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            publication = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Tracks.Where(t => t.BiblePublicationSectionId == null))
                .Where(x => x.PublicationCode == publicationCode && x.LanguageId == null)
                .FirstOrDefaultAsync();
        }
        else
        {
            publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode);
        }

        Log.Debug("GetFirstTrackForNonSectionedAsync: Loaded publication={PublicationName}, TracksCount={TracksCount}",
            publication?.Name ?? "(null)", publication?.Tracks?.Count ?? 0);

        // If no tracks found, ensure publication is harvested (for non-sectioned publications, this fetches tracks)
        if (publication == null || publication.Tracks == null || publication.Tracks.Count == 0)
        {
            Log.Information("GetFirstTrackForNonSectionedAsync: No tracks found in database, ensuring publication exists (will fetch tracks for non-sectioned publications)...");

            if (!string.IsNullOrEmpty(languageCode) && languageContentService != null)
            {
                progress?.UpdateProgress(0.6);
                var harvestSuccess = await languageContentService.EnsurePublicationExistsAsync(publicationCode, languageCode, default, progress);

                if (harvestSuccess)
                {
                    Log.Debug("GetFirstTrackForNonSectionedAsync: Publication harvested successfully, re-querying tracks");
                    progress?.UpdateProgress(0.8);
                    publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode);
                }
                else
                {
                    Log.Warning("GetFirstTrackForNonSectionedAsync: Failed to harvest publication={PublicationCode} for language={LanguageCode}",
                        publicationCode, languageCode);
                }
            }
            else if (string.IsNullOrEmpty(languageCode))
            {
                // For publications without language (like "iam"), tracks should already be pre-harvested
                Log.Warning("GetFirstTrackForNonSectionedAsync: No tracks found for publication without language={PublicationCode}",
                    publicationCode);
            }
        }

        if (publication == null || publication.Tracks == null || publication.Tracks.Count == 0)
        {
            Log.Warning("GetFirstTrackForNonSectionedAsync: No tracks found for language={LanguageCode}, publication={PublicationCode}",
                languageCode ?? "(null)", publicationCode);
            return (0, 0, string.Empty, string.Empty);
        }

        var firstTrack = publication.Tracks.OrderBy(t => t.Number).First();
        Log.Information("GetFirstTrackForNonSectionedAsync: Found first track Number={TrackNumber}, Title={TrackTitle}",
            firstTrack.Number, firstTrack.Title);

        progress?.UpdateProgress(1.0);
        return (0, firstTrack.Number, string.Empty, firstTrack.Title);
    }

    internal async Task<bool> CheckIfPublicationWithFirstSectionHarvestedAsync(string publicationCode, string languageCode)
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
                    ? "Dramas"
                    : "DramaticBibleReadings";
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
                .OrderBy(sl => sl.SectionCode)
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
            Log.Warning(ex, "CheckIfPublicationWithFirstSectionHarvestedAsync: Error checking if publication {PublicationCode} is harvested",
                publicationCode);
            return false;
        }
    }
}

