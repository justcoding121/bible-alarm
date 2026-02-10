#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;

internal sealed class VocalMusicFirstPublicationTrackSelector
{
    private readonly IMediaService mediaService;
    private readonly IBiblePublicationService? biblePublicationService;
    private readonly ILanguageContentService? languageContentService;
    private readonly IServiceScopeFactory? scopeFactory;

    public VocalMusicFirstPublicationTrackSelector(
        IMediaService mediaService,
        IBiblePublicationService? biblePublicationService,
        ILanguageContentService? languageContentService,
        IServiceScopeFactory? scopeFactory)
    {
        this.mediaService = mediaService;
        this.biblePublicationService = biblePublicationService;
        this.languageContentService = languageContentService;
        this.scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Gets the first song publication and track for a language.
    /// Progress milestones: 50% after publication+section saved, 100% after tracks saved.
    /// </summary>
    public async Task<(string? PublicationCode, string TrackCode, string TrackName, string PublicationName)> GetFirstSongPublicationAndTrackForLanguageAsync(
        LanguageListViewItemModel language,
        ScheduleStateItem? currentSchedule,
        IFetchProgress? progress = null)
    {
        progress?.UpdateProgress(0.0);
        
        // Step 1: Get the first publication code by ID order from PublicationLanguages for Music category
        // This is the publication that should be downloaded when language is selected
        // Filter out publications without LanguageId (instrumental/melody music) - data-driven, not hard-coded
        string? firstPublicationCode = null;
        if (biblePublicationService != null)
        {
            var availablePublicationCodes =
                await biblePublicationService.GetAvailablePublicationCodesAsync(language.Code, "Music");

            // Get publications without LanguageId from BiblePublications (data-driven)
            if (scopeFactory == null)
            {
                Serilog.Log.Warning("GetFirstSongPublicationAndTrackForLanguageAsync: scopeFactory is null, cannot filter publications without LanguageId");
                return (null, string.Empty, string.Empty, string.Empty);
            }

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var publicationsWithoutLanguage = await db.BiblePublications
                .AsNoTracking()
                .Where(bp => bp.Category != null &&
                            bp.Category.CategoryName == "Music" &&
                            bp.LanguageId == null)
                .Select(bp => bp.PublicationCode)
                .Distinct()
                .ToListAsync();

            // Filter out publications without LanguageId
            var vocalPublicationCodes = availablePublicationCodes
                .Where(code => !publicationsWithoutLanguage.Contains(code, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (vocalPublicationCodes.Count > 0)
            {
                // Get first publication code by ID order from PublicationLanguages
                // Check if it has LanguageId, if not, get next one
                var publicationLanguages = await db.PublicationLanguages
                    .AsNoTracking()
                    .Where(pl => pl.Language != null &&
                               pl.Language.LanguageCode == language.Code.ToUpperInvariant() &&
                               pl.Category != null &&
                               pl.Category.CategoryName == "Music")
                    .OrderBy(pl => pl.Id)
                    .ToListAsync();

                // Avoid N+1: batch-load which publications are present with LanguageId for this language.
                var normalizedLanguageCode = language.Code.ToUpperInvariant();
                var candidateCodes = publicationLanguages
                    .Select(pl => pl.PublicationCode)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var codesWithLanguageId = await db.BiblePublications
                    .AsNoTracking()
                    .Where(bp => candidateCodes.Contains(bp.PublicationCode) &&
                                 bp.LanguageId != null &&
                                 bp.Language != null &&
                                 bp.Language.LanguageCode == normalizedLanguageCode)
                    .Select(bp => bp.PublicationCode)
                    .Distinct()
                    .ToListAsync();

                var codesWithLanguageIdSet = codesWithLanguageId.ToHashSet(StringComparer.OrdinalIgnoreCase);

                firstPublicationCode = publicationLanguages
                    .Select(pl => pl.PublicationCode)
                    .FirstOrDefault(code => codesWithLanguageIdSet.Contains(code));

                // Fallback: use first from vocal list if no publication with LanguageId found
                if (string.IsNullOrEmpty(firstPublicationCode))
                {
                    firstPublicationCode = vocalPublicationCodes.FirstOrDefault();
                }
            }
        }

        if (string.IsNullOrEmpty(firstPublicationCode))
        {
            Serilog.Log.Warning("GetFirstSongPublicationAndTrackForLanguageAsync: No first publication found for language={LanguageCode}", language.Code);
            return (null, string.Empty, string.Empty, string.Empty);
        }

        Serilog.Log.Debug("GetFirstSongPublicationAndTrackForLanguageAsync: First publication by ID order={PublicationCode} for language={LanguageCode}",
            firstPublicationCode, language.Code);

        // Step 2: Download the first publication with its first section (if sectioned) and tracks
        // This happens when language is selected (cascade)
        // EnsurePublicationExistsAsync reports: 50% (pub+section saved), 100% (tracks saved)
        if (languageContentService != null && !language.Code.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            Serilog.Log.Information("Downloading first vocal music publication {PublicationCode} (by ID order) for language {LanguageCode} (cascade)",
                firstPublicationCode, language.Code);

            try
            {
                // EnsurePublicationExistsAsync downloads the publication with its first section (by ID order) and tracks
                // It reports progress: 0.5 (pub+section saved), 1.0 (tracks saved)
                var fetchSuccess = await languageContentService.EnsurePublicationExistsAsync(
                    firstPublicationCode, language.Code, default, progress);

                if (!fetchSuccess)
                {
                    Serilog.Log.Warning("Failed to download first vocal music publication {PublicationCode} for language {LanguageCode}",
                        firstPublicationCode, language.Code);
                }
                else
                {
                    // Ensure subsequent GetBiblePublications() sees fresh (non-placeholder) data.
                    // Otherwise the cache can keep returning stale placeholder names after a successful harvest.
                    mediaService.InvalidateBiblePublicationsCache(language.Code, "Music");
                }
            }
            catch (Exception ex)
            {
                if (NetworkExceptionHelper.IsNetworkFailure(ex))
                {
                    throw;
                }

                Serilog.Log.Warning(ex, "Error downloading first vocal music publication {PublicationCode} for language {LanguageCode}",
                    firstPublicationCode, language.Code);
            }
        }

        // Step 3: Get the downloaded publication using GetBiblePublications (same API as Bible publication)
        // Data is already saved, just reading from DB - no progress updates needed
        var songPublications = await mediaService.GetBiblePublications(language.Code, "Music", downloadAll: false, null);

        if (songPublications == null || songPublications.Count == 0)
        {
            return (null, string.Empty, string.Empty, string.Empty);
        }

        static bool IsHarvestedPublication(Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublication publication)
        {
            if (publication.LanguageId == null)
            {
                return false;
            }

            if (publication.Id <= 0)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(publication.Name))
            {
                return false;
            }

            return !publication.Name.Equals(publication.PublicationCode, StringComparison.OrdinalIgnoreCase);
        }

        // Prefer the publication we just ensured exists, and ignore placeholders.
        var firstSongPublication = songPublications.Values
            .FirstOrDefault(p =>
                IsHarvestedPublication(p) &&
                p.PublicationCode.Equals(firstPublicationCode, StringComparison.OrdinalIgnoreCase))
            ?? songPublications.Values
                .Where(IsHarvestedPublication)
                .OrderBy(p => p.Id)
                .FirstOrDefault();

        if (firstSongPublication == null)
        {
            return (null, string.Empty, string.Empty, string.Empty);
        }

        var publicationCode = firstSongPublication.PublicationCode;
        var isSameLanguage = IsSameLanguageAndSongPublication(currentSchedule, language.Code, publicationCode);

        // Use GetBiblePublicationTracks for vocal music (same API as Bible publication)
        // For vocal music, we need to get tracks from the first section (or flat publication)
        // IMPORTANT: during "language change" cascade we should NOT ensure all sections.
        // Ensuring all sections is reserved for when the user explicitly opens the Sections modal.
        // We only need the already-downloaded first section (if sectioned).
        // Data is already saved, just reading from DB - no progress updates needed
        var sections = await mediaService.GetBiblePublicationSections(language.Code, publicationCode, progress: null);

        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationTrack>? tracks;
        if (sections != null && sections.Count > 0)
        {
            // Sectioned publication - get tracks from first section
            var firstSection = sections.First();
            tracks = await mediaService.GetBiblePublicationTracks(language.Code, publicationCode, firstSection.Value.SectionCode);
        }
        else
        {
            // Flat publication - get tracks directly (no sections)
            tracks = await mediaService.GetBiblePublicationTracks(language.Code, publicationCode, null);
        }

        if (tracks == null || tracks.Count == 0)
        {
            return (null, string.Empty, string.Empty, string.Empty);
        }

        string trackCode;
        string trackName;

        if (isSameLanguage &&
            !string.IsNullOrWhiteSpace(currentSchedule?.MusicTrackCode) &&
            tracks.TryGetValue(currentSchedule.MusicTrackCode, out var currentTrack))
        {
            trackCode = currentSchedule.MusicTrackCode;
            trackName = currentTrack.Title;
        }
        else
        {
            var tracksList = tracks.Values.ToList();
            var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
            trackCode = TrackCodeHelper.GetFromTrack(randomTrack);
            trackName = randomTrack.Title;
        }

        return (publicationCode, trackCode, trackName, firstSongPublication.Name);
    }

    private static bool IsSameLanguageAndSongPublication(ScheduleStateItem? currentSchedule, string languageCode, string publicationCode)
    {
        // Vocal music has a non-null LanguageCode
        return currentSchedule != null &&
               !string.IsNullOrEmpty(currentSchedule.MusicLanguageCode) &&
               currentSchedule.MusicLanguageCode == languageCode &&
               currentSchedule.MusicPublicationCode == publicationCode;
    }
}

