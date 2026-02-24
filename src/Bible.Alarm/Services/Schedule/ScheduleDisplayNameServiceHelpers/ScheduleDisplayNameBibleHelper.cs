#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;
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
                logger.Warning(ex, "Error populating BiblePublicationLanguageName");
                scheduleStateItem.BiblePublicationLanguageName = scheduleLanguageCode;
                scheduleStateItem.BiblePublicationLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
            }
        }

        if (!string.IsNullOrWhiteSpace(publicationCode))
        {
            try
            {
                var hasSections = PublicationTypeHelper.HasSectionStructure(publicationCode);
                Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublication? publication = null;
                var publicationWithoutLanguage = false;

                if (!string.IsNullOrWhiteSpace(scheduleLanguageCode) && biblePublicationService != null)
                {
                    publication = hasSections
                        ? await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(scheduleLanguageCode, publicationCode)
                        : await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(scheduleLanguageCode, publicationCode);
                }

                if (publication == null)
                {
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
                        publicationWithoutLanguage = publication != null;
                        if (publicationWithoutLanguage)
                            logger.Debug("No-language publication {PublicationCode}; keeping BiblePublicationLanguageName for display (LanguageCode: {LanguageCode})", publicationCode, scheduleLanguageCode ?? "null");
                    }
                    catch (Exception ex)
                    {
                        logger.Warning(ex, "Error loading publication without language FK from media index (PublicationCode={PublicationCode})", publicationCode);
                    }
                }

                if (publication != null)
                {
                    if (!string.IsNullOrWhiteSpace(publication.Name))
                        scheduleStateItem.BiblePublicationName = publication.Name;
                    scheduleStateItem.BiblePublicationIsMusic = publication.IsMusic;
                    if (publication.PrimaryCategory != null)
                    {
                        scheduleStateItem.BiblePublicationCategoryId = publication.PrimaryCategoryId;
                        scheduleStateItem.BiblePublicationCategoryName = publication.PrimaryCategory.CategoryCode;
                        logger.Debug("Populated BiblePublicationCategoryId={CategoryId}, BiblePublicationCategoryName={CategoryName} for schedule {ScheduleId}", publication.PrimaryCategoryId, publication.PrimaryCategory.CategoryCode, scheduleStateItem.Id);
                    }
                    else
                    {
                        var categoryCode = JwSourceHelper.GetCategoryCode(publicationCode);
                        if (!string.IsNullOrWhiteSpace(categoryCode))
                        {
                            scheduleStateItem.BiblePublicationCategoryName = categoryCode;
                            logger.Debug("Populated BiblePublicationCategoryName={CategoryName} from publication code for schedule {ScheduleId}", categoryCode, scheduleStateItem.Id);
                        }
                    }
                    if (!hasSections && !string.IsNullOrWhiteSpace(biblePublicationSchedule.TrackCode) && publication.Tracks != null && publication.Tracks.Count > 0)
                    {
                        var track = publication.Tracks.FirstOrDefault(t => t.TrackCode == biblePublicationSchedule.TrackCode);
                        if (track != null && !string.IsNullOrWhiteSpace(track.Title))
                            scheduleStateItem.BiblePublicationTrackTitle = track.Title;
                    }
                    if (publicationWithoutLanguage && string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationCategoryName))
                    {
                        var categoryName = JwSourceHelper.GetCategoryName(publicationCode);
                        if (!string.IsNullOrWhiteSpace(categoryName))
                            scheduleStateItem.BiblePublicationCategoryName = categoryName;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating BiblePublicationName and Category");
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

        var normalizedSectionCode = SectionCodeHelper.Normalize(biblePublicationSchedule.SectionCode);
        if (string.IsNullOrWhiteSpace(normalizedSectionCode))
            scheduleStateItem.BiblePublicationSectionName = null;
        else if (!string.IsNullOrWhiteSpace(publicationCode))
        {
            try
            {
                try
                {
                    var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
                    using var scope = scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
                    var sectionCodeLower = normalizedSectionCode.ToLowerInvariant();
                    var sectionName = await dbContext.BiblePublicationSections
                        .AsNoTracking()
                        .Where(x => x.BiblePublication.PublicationCode == publicationCode && x.BiblePublication.LanguageId == null && x.SectionCode != null && x.SectionCode.ToLower() == sectionCodeLower)
                        .Select(x => x.Name)
                        .FirstOrDefaultAsync();
                    if (!string.IsNullOrWhiteSpace(sectionName))
                        scheduleStateItem.BiblePublicationSectionName = sectionName;
                }
                catch (Exception ex)
                {
                    logger.Debug(ex, "No-language section lookup failed (PublicationCode={PublicationCode}, SectionCode={SectionCode})", publicationCode, normalizedSectionCode);
                }
                if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationSectionName) && !string.IsNullOrWhiteSpace(scheduleLanguageCode))
                {
                    var biblePublicationSectionService = serviceProvider.GetRequiredService<IBiblePublicationSectionService>();
                    var sectionName = await Task.Run(async () => await biblePublicationSectionService.GetSectionNameAsync(scheduleLanguageCode, publicationCode, normalizedSectionCode));
                    if (!string.IsNullOrWhiteSpace(sectionName))
                        scheduleStateItem.BiblePublicationSectionName = sectionName;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating BiblePublicationSectionName");
            }
        }

        if (!string.IsNullOrWhiteSpace(biblePublicationSchedule.TrackCode) && !string.IsNullOrWhiteSpace(publicationCode))
        {
            try
            {
                if (PublicationTypeHelper.HasSectionStructure(publicationCode))
                {
                    var sectionCodeForTracks = SectionCodeHelper.Normalize(biblePublicationSchedule.SectionCode);
                    if (!string.IsNullOrWhiteSpace(sectionCodeForTracks))
                    {
                        var categoryName = scheduleStateItem.BiblePublicationCategoryName ?? JwSourceHelper.GetCategoryName(publicationCode) ?? string.Empty;
                        if (string.Equals(categoryName, "Music", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var melodyTracks = await mediaService.GetMelodyMusicTracksBySection(publicationCode, sectionCodeForTracks);
                                if (!string.IsNullOrWhiteSpace(biblePublicationSchedule.TrackCode) &&
                                    int.TryParse(biblePublicationSchedule.TrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var melodyTrackNum) &&
                                    melodyTracks.TryGetValue(melodyTrackNum, out var melodyTrack) && melodyTrack != null)
                                {
                                    var normalized = DisplayNameNormalizer.NormalizeTrackTitle(melodyTrack.Title);
                                    if (!string.IsNullOrWhiteSpace(normalized))
                                    {
                                        scheduleStateItem.BiblePublicationTrackTitle = normalized;
                                        return;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Debug(ex, "Failed to resolve melody track title (PublicationCode={PublicationCode}, SectionCode={SectionCode}, TrackCode={TrackCode})", publicationCode, sectionCodeForTracks, biblePublicationSchedule.TrackCode);
                            }
                        }
                        var languageForTracks = scheduleLanguageCode ?? string.Empty;
                        var tracks = await Task.Run(async () => await mediaService.GetBiblePublicationTracks(languageForTracks, publicationCode, sectionCodeForTracks));
                        if (tracks == null || tracks.Count == 0)
                            tracks = await Task.Run(async () => await mediaService.GetBiblePublicationTracks(string.Empty, publicationCode, sectionCodeForTracks));
                        if (tracks != null && !string.IsNullOrWhiteSpace(biblePublicationSchedule.TrackCode) && tracks.TryGetValue(biblePublicationSchedule.TrackCode, out var track) && !string.IsNullOrWhiteSpace(track.Title))
                            scheduleStateItem.BiblePublicationTrackTitle = DisplayNameNormalizer.NormalizeTrackTitle(track.Title) ?? track.Title;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating BiblePublicationTrackTitle");
            }
            if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationTrackTitle))
            {
                var categoryName = scheduleStateItem.BiblePublicationCategoryName ?? JwSourceHelper.GetCategoryName(publicationCode) ?? string.Empty;
                scheduleStateItem.BiblePublicationTrackTitle = string.Equals(categoryName, "Music", StringComparison.OrdinalIgnoreCase)
                    ? $"Track {biblePublicationSchedule.TrackCode}"
                    : PublicationTypeHelper.HasSectionStructure(publicationCode) ? $"Chapter {biblePublicationSchedule.TrackCode}" : $"Track {biblePublicationSchedule.TrackCode}";
            }
        }
    }
}
