#nullable enable

using System;
using System.Linq;
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

        var firstPublicationCode = await ResolveFirstSongPublicationCodeAsync(language);

        if (string.IsNullOrEmpty(firstPublicationCode))
        {
            Serilog.Log.Warning(AppConstants.Logging.VocalMusicFirstSongCascadeDiagnosticsLog.NoFirstPublicationForLanguage, language.Code);
            return (null, string.Empty, string.Empty, string.Empty);
        }

        Serilog.Log.Debug(AppConstants.Logging.VocalMusicFirstSongCascadeDiagnosticsLog.FirstPublicationByIdOrder,
            firstPublicationCode, language.Code);

        await CascadeDownloadFirstVocalPublicationWhenNeededAsync(language, firstPublicationCode, progress);

        var songPublications = await mediaService.GetBiblePublications(language.Code, AppConstants.Media.BiblePublicationCategoryMusic, downloadAll: false, null, requireIsMusicForMusicCategory: true);

        if (songPublications == null || songPublications.Count == 0)
        {
            return (null, string.Empty, string.Empty, string.Empty);
        }

        var firstSongPublication = PickFirstCatalogedSongPublication(songPublications, firstPublicationCode);

        if (firstSongPublication == null)
        {
            return (null, string.Empty, string.Empty, string.Empty);
        }

        var publicationCode = firstSongPublication.PublicationCode;
        var tracks = await LoadVocalPublicationTracksAsync(language.Code, publicationCode);

        if (tracks == null || tracks.Count == 0)
        {
            return (null, string.Empty, string.Empty, string.Empty);
        }

        var (trackCode, trackName) = PickVocalTrackForCascade(currentSchedule, language.Code, publicationCode, tracks);

        return (publicationCode, trackCode, trackName, firstSongPublication.Name);
    }

    private async Task CascadeDownloadFirstVocalPublicationWhenNeededAsync(
        LanguageListViewItemModel language,
        string firstPublicationCode,
        IFetchProgress? progress)
    {
        // EnsurePublicationExistsAsync reports: 50% (pub+section saved), 100% (tracks saved)
        if (languageContentService == null ||
            language.Code.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Serilog.Log.Information(AppConstants.Logging.VocalMusicFirstSongCascadeDiagnosticsLog.DownloadingFirstVocalPublicationCascade,
            firstPublicationCode, language.Code);

        try
        {
            var fetchSuccess = await languageContentService.EnsurePublicationExistsAsync(
                firstPublicationCode, language.Code, progress);

            if (!fetchSuccess)
            {
                Serilog.Log.Warning(AppConstants.Logging.VocalMusicFirstSongCascadeDiagnosticsLog.FailedToDownloadFirstVocalPublication,
                    firstPublicationCode, language.Code);
            }
            else
            {
                mediaService.InvalidateBiblePublicationsCache(language.Code, AppConstants.Media.BiblePublicationCategoryMusic);
            }
        }
        catch (Exception ex)
        {
            if (NetworkExceptionHelper.IsNetworkFailure(ex))
            {
                throw;
            }

            Serilog.Log.Warning(ex, AppConstants.Logging.VocalMusicFirstSongCascadeDiagnosticsLog.ErrorDownloadingFirstVocalPublication,
                firstPublicationCode, language.Code);
        }
    }

    private static bool IsCatalogedSongPublication(Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublication publication)
    {
        if (publication.LanguageId == null || publication.Id <= 0 || string.IsNullOrWhiteSpace(publication.Name))
        {
            return false;
        }

        return !publication.Name.Equals(publication.PublicationCode, StringComparison.OrdinalIgnoreCase);
    }

    private static Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublication? PickFirstCatalogedSongPublication(
        Dictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublication> songPublications,
        string firstPublicationCode)
    {
        return songPublications.Values
            .FirstOrDefault(p =>
                IsCatalogedSongPublication(p) &&
                PublicationCodeHelper.CodeEquals(p.PublicationCode, firstPublicationCode))
            ?? songPublications.Values
                .Where(IsCatalogedSongPublication)
                .OrderBy(p => p.Id)
                .FirstOrDefault();
    }

    private async Task<SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationTrack>?> LoadVocalPublicationTracksAsync(
        string languageCode,
        string publicationCode)
    {
        var sections = await mediaService.GetBiblePublicationSections(languageCode, publicationCode, progress: null);

        if (sections != null && sections.Count > 0)
        {
            using var sectionEnumerator = sections.GetEnumerator();
            _ = sectionEnumerator.MoveNext();
            var firstSection = sectionEnumerator.Current;
            return await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, firstSection.Value.SectionCode);
        }

        return await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, null);
    }

    private static (string TrackCode, string TrackName) PickVocalTrackForCascade(
        ScheduleStateItem? currentSchedule,
        string languageCode,
        string publicationCode,
        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationTrack> tracks)
    {
        if (IsSameLanguageAndSongPublication(currentSchedule, languageCode, publicationCode) &&
            currentSchedule!.MusicTrackCode is { Length: > 0 } persistedCode &&
            tracks.TryGetValue(persistedCode, out var currentTrack))
        {
            return (persistedCode, currentTrack.Title);
        }

        var tracksList = tracks.Values.ToList();
        var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
        return (TrackCodeHelper.GetFromTrack(randomTrack), randomTrack.Title);
    }

    private async Task<string?> ResolveFirstSongPublicationCodeAsync(LanguageListViewItemModel language)
    {
        if (biblePublicationService == null)
        {
            return null;
        }

        var availablePublicationCodes =
            await biblePublicationService.GetAvailablePublicationCodesAsync(language.Code, AppConstants.Media.BiblePublicationCategoryMusic, true);

        if (scopeFactory == null)
        {
            Serilog.Log.Warning(AppConstants.Logging.VocalMusicFirstSongCascadeDiagnosticsLog.ScopeFactoryNullCannotFilterPublicationsWithoutLanguageId);
            return null;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var publicationsWithoutLanguage = await db.BiblePublications
            .AsNoTracking()
            .Where(bp => bp.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == AppConstants.Media.BiblePublicationCategoryMusic) &&
                        bp.LanguageId == null)
            .Select(bp => bp.PublicationCode)
            .Distinct()
            .ToListAsync();

        var vocalPublicationCodes = availablePublicationCodes
            .Where(code => !publicationsWithoutLanguage.Contains(code, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (vocalPublicationCodes.Count == 0)
        {
            return null;
        }

        var musicComparer = PublicationCodeHelper.GetPublicationCodeComparerForCategory(AppConstants.Media.BiblePublicationCategoryMusic);
        var publicationLanguages = (await db.PublicationLanguages
                .AsNoTracking()
                .Where(pl => pl.Language != null &&
                           string.Equals(pl.Language.LanguageCode, language.Code, StringComparison.OrdinalIgnoreCase) &&
                           pl.Category != null &&
                           pl.Category.CategoryCode == AppConstants.Media.BiblePublicationCategoryMusic)
                .ToListAsync())
            .OrderBy(pl => pl.PublicationCode, musicComparer)
            .ThenBy(pl => pl.Id)
            .ToList();

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

        var firstPublicationCode = publicationLanguages
            .Select(pl => pl.PublicationCode)
            .FirstOrDefault(code => codesWithLanguageIdSet.Contains(code));

        if (string.IsNullOrEmpty(firstPublicationCode))
        {
            firstPublicationCode = vocalPublicationCodes[0];
        }

        return firstPublicationCode;
    }

    private static bool IsSameLanguageAndSongPublication(ScheduleStateItem? currentSchedule, string languageCode, string publicationCode)
    {
        return currentSchedule != null &&
               !string.IsNullOrEmpty(currentSchedule.MusicLanguageCode) &&
               string.Equals(currentSchedule.MusicLanguageCode, languageCode, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(currentSchedule.MusicPublicationCode, publicationCode, StringComparison.OrdinalIgnoreCase);
    }
}

