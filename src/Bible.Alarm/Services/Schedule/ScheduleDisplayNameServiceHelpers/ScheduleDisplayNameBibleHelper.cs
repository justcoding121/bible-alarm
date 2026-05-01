#nullable enable

using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Services.Schedule.ScheduleDisplayNameServiceHelpers;

public sealed class ScheduleDisplayNameBibleHelper
{
    private readonly ILogger logger;
    private readonly IBiblePublicationService? biblePublicationService;
    private readonly IMediaService mediaService;
    private readonly ILanguageNameService languageNameService;
    private readonly IServiceProvider serviceProvider;

    public ScheduleDisplayNameBibleHelper(
        ILogger logger,
        IBiblePublicationService? biblePublicationService,
        IMediaService mediaService,
        ILanguageNameService languageNameService,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.biblePublicationService = biblePublicationService;
        this.mediaService = mediaService;
        this.languageNameService = languageNameService;
        this.serviceProvider = serviceProvider;
    }

    public async Task PopulateAsync(ScheduleStateItem scheduleStateItem, BiblePublicationSchedule biblePublicationSchedule)
    {
        var scheduleLanguageCode = biblePublicationSchedule.LanguageCode;
        var publicationCode = biblePublicationSchedule.PublicationCode;

        await PopulateBibleLanguageFieldsAsync(scheduleStateItem, scheduleLanguageCode);

        await PopulatePublicationCategoryAndTracksAsync(scheduleStateItem, biblePublicationSchedule, scheduleLanguageCode, publicationCode);

        ApplyBiblePublicationFallbackNames(scheduleStateItem, publicationCode);

        await PopulateBibleSectionFieldsAsync(scheduleStateItem, biblePublicationSchedule, scheduleLanguageCode, publicationCode);

        await PopulateBibleTrackTitleFieldsAsync(scheduleStateItem, biblePublicationSchedule, scheduleLanguageCode, publicationCode);
    }

    private async Task PopulateBibleLanguageFieldsAsync(ScheduleStateItem scheduleStateItem, string? scheduleLanguageCode)
    {
        if (!string.IsNullOrWhiteSpace(scheduleLanguageCode) && biblePublicationService != null)
        {
            try
            {
                var languagesDict = await biblePublicationService.GetDistinctLanguagesAsync();
                if (languagesDict.TryGetValue(scheduleLanguageCode, out var language))
                {
                    scheduleStateItem.BiblePublicationLanguageName = languageNameService.GetNameCached(language.Id) ?? scheduleLanguageCode;
                    scheduleStateItem.BiblePublicationLanguageDirection = language.Direction;
                }
                else
                {
                    scheduleStateItem.BiblePublicationLanguageName = scheduleLanguageCode;
                    scheduleStateItem.BiblePublicationLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, AppConstants.Logging.ScheduleDisplayNameBibleHelperDiagnosticsLog.ErrorPopulatingBiblePublicationLanguageName);
                scheduleStateItem.BiblePublicationLanguageName = scheduleLanguageCode;
                scheduleStateItem.BiblePublicationLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
            }
        }
    }

    private async Task PopulatePublicationCategoryAndTracksAsync(
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublicationSchedule,
        string? scheduleLanguageCode,
        string publicationCode)
    {
        if (string.IsNullOrWhiteSpace(publicationCode))
        {
            return;
        }

        try
        {
            var hasSections = PublicationTypeHelper.HasSectionStructure(publicationCode);
            var (publication, publicationWithoutLanguage) =
                await TryLoadPublicationForDisplayLookupAsync(publicationCode, scheduleLanguageCode, hasSections);

            if (publication != null)
            {
                ApplyLoadedPublicationMetadataToSchedule(scheduleStateItem, biblePublicationSchedule, publicationCode,
                    scheduleLanguageCode, hasSections, publication, publicationWithoutLanguage);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.ScheduleDisplayNameBibleHelperDiagnosticsLog.ErrorPopulatingBiblePublicationNameAndCategory);
        }
    }

    private async Task<(BiblePublication? Publication, bool PublicationWithoutLanguage)> TryLoadPublicationForDisplayLookupAsync(
        string publicationCode,
        string? scheduleLanguageCode,
        bool hasSections)
    {
        BiblePublication? publication = null;

        if (!string.IsNullOrWhiteSpace(scheduleLanguageCode) && biblePublicationService != null)
        {
            publication = hasSections
                ? await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(scheduleLanguageCode, publicationCode)
                : await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(scheduleLanguageCode, publicationCode);
        }

        if (publication != null)
        {
            return (publication, false);
        }

        try
        {
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            publication = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.BiblePublicationCategories)
                .ThenInclude(bpc => bpc.Category)
                .Where(x => x.PublicationCode == publicationCode && x.LanguageId == null)
                .FirstOrDefaultAsync();

            var publicationWithoutLanguage = publication != null;
            if (publicationWithoutLanguage)
            {
                logger.Debug(AppConstants.Logging.ScheduleDisplayNameBibleHelperDiagnosticsLog.NoLanguagePublicationKeepingLanguageNameForDisplay,
                    publicationCode, scheduleLanguageCode ?? "null");
            }

            return (publication, publicationWithoutLanguage);
        }
        catch (Exception ex)
        {
            logger.Warning(ex,
                AppConstants.Logging.ScheduleDisplayNameBibleHelperDiagnosticsLog.ErrorLoadingPublicationWithoutLanguageFkFromMediaIndex,
                publicationCode);
            return (null, false);
        }
    }

    private void ApplyLoadedPublicationMetadataToSchedule(
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublicationSchedule,
        string publicationCode,
        string? scheduleLanguageCode,
        bool hasSections,
        BiblePublication publication,
        bool publicationWithoutLanguage)
    {
        if (!string.IsNullOrWhiteSpace(publication.Name))
        {
            scheduleStateItem.BiblePublicationName = publication.Name;
        }

        scheduleStateItem.BiblePublicationIsMusic = publication.IsMusic ||
                                                    JwSourceHelper.MusicFlagPublicationCodes.Contains(publicationCode);

        if (publication.PrimaryCategory != null)
        {
            scheduleStateItem.BiblePublicationCategoryId = publication.PrimaryCategoryId;
            scheduleStateItem.BiblePublicationCategoryName = publication.PrimaryCategory.CategoryCode;
            logger.Debug(AppConstants.Logging.ScheduleDisplayNameBibleHelperDiagnosticsLog.PopulatedBiblePublicationCategoryIdAndCategoryName,
                publication.PrimaryCategoryId, publication.PrimaryCategory.CategoryCode, scheduleStateItem.Id);
        }
        else
        {
            var categoryCode = JwSourceHelper.GetCategoryCode(publicationCode);
            if (!string.IsNullOrWhiteSpace(categoryCode))
            {
                scheduleStateItem.BiblePublicationCategoryName = categoryCode;
                logger.Debug(AppConstants.Logging.ScheduleDisplayNameBibleHelperDiagnosticsLog.PopulatedBiblePublicationCategoryNameFromPublicationCode,
                    categoryCode, scheduleStateItem.Id);
            }
        }

        if (!hasSections && !string.IsNullOrWhiteSpace(biblePublicationSchedule.TrackCode) && publication.Tracks != null &&
            publication.Tracks.Count > 0)
        {
            var track = publication.Tracks.FirstOrDefault(t =>
                CodeComparisonHelper.Equals(t.TrackCode, biblePublicationSchedule.TrackCode));
            if (track != null && !string.IsNullOrWhiteSpace(track.Title))
            {
                scheduleStateItem.BiblePublicationTrackTitle = track.Title;
            }
        }

        if (publicationWithoutLanguage && string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationCategoryName))
        {
            var categoryName = JwSourceHelper.GetCategoryName(publicationCode);
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                scheduleStateItem.BiblePublicationCategoryName = categoryName;
            }
        }
    }

    private static void ApplyBiblePublicationFallbackNames(ScheduleStateItem scheduleStateItem, string publicationCode)
    {
        if (string.IsNullOrWhiteSpace(publicationCode))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationName))
            scheduleStateItem.BiblePublicationName = publicationCode;
        if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationCategoryName))
        {
            var categoryName = JwSourceHelper.GetCategoryName(publicationCode);
            if (!string.IsNullOrWhiteSpace(categoryName))
                scheduleStateItem.BiblePublicationCategoryName = categoryName;
        }
    }

    private async Task PopulateBibleSectionFieldsAsync(
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublicationSchedule,
        string? scheduleLanguageCode,
        string publicationCode)
    {
        var normalizedSectionCode = SectionCodeHelper.Normalize(biblePublicationSchedule.SectionCode);
        if (string.IsNullOrWhiteSpace(normalizedSectionCode))
        {
            scheduleStateItem.BiblePublicationSectionName = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(publicationCode))
        {
            return;
        }

        try
        {
            await TryPopulateSectionNameFromPublicationWithoutLanguageAsync(
                scheduleStateItem,
                publicationCode,
                normalizedSectionCode);

            await TryPopulateSectionNameFromLanguageServiceAsync(
                scheduleStateItem,
                scheduleLanguageCode,
                publicationCode,
                normalizedSectionCode);
        }
        catch (Exception ex)
        {
            logger.Warning(ex,
                AppConstants.Logging.ScheduleDisplayNameBibleHelperDiagnosticsLog.ErrorPopulatingBiblePublicationSectionName);
        }
    }

    private async Task TryPopulateSectionNameFromPublicationWithoutLanguageAsync(
        ScheduleStateItem scheduleStateItem,
        string publicationCode,
        string normalizedSectionCode)
    {
        try
        {
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            var sectionName = await dbContext.BiblePublicationSections
                .AsNoTracking()
                .Where(x => x.BiblePublication.PublicationCode == publicationCode && x.BiblePublication.LanguageId == null &&
                            x.SectionCode != null && string.Equals(x.SectionCode, normalizedSectionCode, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Name)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(sectionName))
            {
                scheduleStateItem.BiblePublicationSectionName = sectionName;
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, AppConstants.Logging.ScheduleDisplayNameBibleHelperDiagnosticsLog.NoLanguageSectionLookupFailed, publicationCode,
                normalizedSectionCode);
        }
    }

    private async Task TryPopulateSectionNameFromLanguageServiceAsync(
        ScheduleStateItem scheduleStateItem,
        string? scheduleLanguageCode,
        string publicationCode,
        string normalizedSectionCode)
    {
        if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationSectionName) &&
            !string.IsNullOrWhiteSpace(scheduleLanguageCode))
        {
            var biblePublicationSectionService = serviceProvider.GetRequiredService<IBiblePublicationSectionService>();
            var sectionName = await Task.Run(async () =>
                await biblePublicationSectionService.GetSectionNameAsync(scheduleLanguageCode, publicationCode, normalizedSectionCode));
            if (!string.IsNullOrWhiteSpace(sectionName))
            {
                scheduleStateItem.BiblePublicationSectionName = sectionName;
            }
        }
    }

    private async Task PopulateBibleTrackTitleFieldsAsync(
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublicationSchedule,
        string? scheduleLanguageCode,
        string publicationCode)
    {
        if (string.IsNullOrWhiteSpace(biblePublicationSchedule.TrackCode) || string.IsNullOrWhiteSpace(publicationCode))
        {
            return;
        }

        try
        {
            if (PublicationTypeHelper.HasSectionStructure(publicationCode))
            {
                await TryPopulateStructuredPublicationTrackTitleAsync(scheduleStateItem, biblePublicationSchedule,
                    publicationCode,
                    scheduleLanguageCode);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex,
                AppConstants.Logging.ScheduleDisplayNameBibleHelperDiagnosticsLog.ErrorPopulatingBiblePublicationTrackTitle);
        }

        if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationTrackTitle))
        {
            AssignBiblePublicationTrackTitleFallback(scheduleStateItem, biblePublicationSchedule, publicationCode);
        }
    }

    private async Task TryPopulateStructuredPublicationTrackTitleAsync(
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublicationSchedule,
        string publicationCode,
        string? scheduleLanguageCode)
    {
        var sectionCodeForTracks = SectionCodeHelper.Normalize(biblePublicationSchedule.SectionCode);
        if (string.IsNullOrWhiteSpace(sectionCodeForTracks))
        {
            return;
        }

        var categoryName =
            scheduleStateItem.BiblePublicationCategoryName ?? JwSourceHelper.GetCategoryName(publicationCode) ??
            string.Empty;

        await TryPopulateMelodyTrackTitleIfMusicCategoryAsync(scheduleStateItem, biblePublicationSchedule, publicationCode,
            sectionCodeForTracks, categoryName);

        if (!string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationTrackTitle))
        {
            return;
        }

        await TryPopulateBiblePublicationTracksDictionaryTitleAsync(scheduleStateItem, biblePublicationSchedule,
            publicationCode, scheduleLanguageCode, sectionCodeForTracks);
    }

    private async Task TryPopulateMelodyTrackTitleIfMusicCategoryAsync(
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublicationSchedule,
        string publicationCode,
        string sectionCodeForTracks,
        string categoryName)
    {
        if (!string.Equals(categoryName, AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var melodyTracks = await mediaService.GetMelodyMusicTracksBySection(publicationCode, sectionCodeForTracks);
            if (!string.IsNullOrWhiteSpace(biblePublicationSchedule.TrackCode) &&
                MusicTrackLookupHelper.TryGetByCode(melodyTracks, biblePublicationSchedule.TrackCode, out var melodyPair))
            {
                var normalized = DisplayNameNormalizer.NormalizeTrackTitle(melodyPair.Track.Title);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    scheduleStateItem.BiblePublicationTrackTitle = normalized;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, AppConstants.Logging.ScheduleDisplayNameBibleHelperDiagnosticsLog.FailedToResolveMelodyTrackTitle,
                publicationCode, sectionCodeForTracks,
                biblePublicationSchedule.TrackCode);
        }
    }

    private async Task TryPopulateBiblePublicationTracksDictionaryTitleAsync(
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublicationSchedule,
        string publicationCode,
        string? scheduleLanguageCode,
        string sectionCodeForTracks)
    {
        var languageForTracks = scheduleLanguageCode ?? string.Empty;
        var tracks = await Task.Run(async () =>
            await mediaService.GetBiblePublicationTracks(languageForTracks, publicationCode, sectionCodeForTracks));
        if (tracks == null || tracks.Count == 0)
        {
            tracks = await Task.Run(async () =>
                await mediaService.GetBiblePublicationTracks(string.Empty, publicationCode, sectionCodeForTracks));
        }

        if (tracks != null && !string.IsNullOrWhiteSpace(biblePublicationSchedule.TrackCode) &&
            tracks.TryGetValue(biblePublicationSchedule.TrackCode, out var track) &&
            !string.IsNullOrWhiteSpace(track.Title))
        {
            scheduleStateItem.BiblePublicationTrackTitle =
                DisplayNameNormalizer.NormalizeTrackTitle(track.Title) ?? track.Title;
        }
    }

    private static void AssignBiblePublicationTrackTitleFallback(
        ScheduleStateItem scheduleStateItem,
        BiblePublicationSchedule biblePublicationSchedule,
        string publicationCode)
    {
        var categoryName =
            scheduleStateItem.BiblePublicationCategoryName ?? JwSourceHelper.GetCategoryName(publicationCode) ??
            string.Empty;
        string fallbackTitle;
        if (string.Equals(categoryName, AppConstants.Media.BiblePublicationCategoryMusic,
                StringComparison.OrdinalIgnoreCase))
        {
            fallbackTitle = $"Track {biblePublicationSchedule.TrackCode}";
        }
        else if (PublicationTypeHelper.HasSectionStructure(publicationCode))
        {
            fallbackTitle = $"Chapter {biblePublicationSchedule.TrackCode}";
        }
        else
        {
            fallbackTitle = $"Track {biblePublicationSchedule.TrackCode}";
        }

        scheduleStateItem.BiblePublicationTrackTitle = fallbackTitle;
    }
}
