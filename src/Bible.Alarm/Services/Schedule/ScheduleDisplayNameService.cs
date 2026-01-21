#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Services.Schedule;

public sealed class ScheduleDisplayNameService : IScheduleDisplayNameService
{
    private readonly ILogger logger;
    private readonly IBiblePublicationService? BiblePublicationService;
    private readonly IMediaService mediaService;
    private readonly IServiceProvider serviceProvider;

    public ScheduleDisplayNameService(
        ILogger logger,
        IBiblePublicationService? BiblePublicationService,
        IMediaService mediaService,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.BiblePublicationService = BiblePublicationService;
        this.mediaService = mediaService;
        this.serviceProvider = serviceProvider;
    }

    public async Task PopulateDisplayNamesAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        // Populate Bible reading display names
        if (schedule.BiblePublicationSchedule != null)
        {
            await PopulateBiblePublicationDisplayNamesAsync(scheduleStateItem, schedule.BiblePublicationSchedule);
        }

        // Populate music display names
        if (schedule.Music != null)
        {
            await PopulateMusicDisplayNamesAsync(scheduleStateItem, schedule.Music);
        }
    }

    private async Task PopulateBiblePublicationDisplayNamesAsync(ScheduleStateItem scheduleStateItem, BiblePublicationSchedule biblePublicationSchedule)
    {
        // Language name and direction
        if (!string.IsNullOrWhiteSpace(biblePublicationSchedule.LanguageCode) && BiblePublicationService != null)
        {
            try
            {
                var languagesDict = await Task.Run(async () =>
                    await BiblePublicationService.GetDistinctLanguagesAsync());
                if (languagesDict.TryGetValue(biblePublicationSchedule.LanguageCode, out var language))
                {
                    scheduleStateItem.BiblePublicationLanguageName = language.Name;
                    scheduleStateItem.BiblePublicationLanguageDirection = language.Direction;
                }
                else
                {
                    scheduleStateItem.BiblePublicationLanguageName = biblePublicationSchedule.LanguageCode;
                    scheduleStateItem.BiblePublicationLanguageDirection = "ltr"; // Default to LTR
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating BiblePublicationLanguageName");
                scheduleStateItem.BiblePublicationLanguageName = biblePublicationSchedule.LanguageCode;
                scheduleStateItem.BiblePublicationLanguageDirection = "ltr"; // Default to LTR
            }
        }

        // Publication name and category
        if (!string.IsNullOrWhiteSpace(biblePublicationSchedule.LanguageCode) &&
            !string.IsNullOrWhiteSpace(biblePublicationSchedule.PublicationCode) &&
            BiblePublicationService != null)
        {
            try
            {
                var publication = await Task.Run(async () =>
                    await BiblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
                        biblePublicationSchedule.LanguageCode,
                        biblePublicationSchedule.PublicationCode));

                if (publication != null)
                {
                    if (!string.IsNullOrWhiteSpace(publication.Name))
                    {
                        scheduleStateItem.BiblePublicationName = publication.Name;
                    }

                    // Populate category from publication
                    if (publication.Category != null)
                    {
                        scheduleStateItem.BiblePublicationCategoryId = publication.CategoryId;
                        scheduleStateItem.BiblePublicationCategoryName = publication.Category.CategoryName;
                        logger.Debug("Populated BiblePublicationCategoryId={CategoryId}, BiblePublicationCategoryName={CategoryName} for schedule {ScheduleId}",
                            publication.CategoryId, publication.Category.CategoryName, scheduleStateItem.Id);
                    }
                    else
                    {
                        // Fallback: derive category name from publication code
                        var categoryName = JwSourceHelper.GetCategoryName(biblePublicationSchedule.PublicationCode);
                        if (!string.IsNullOrWhiteSpace(categoryName))
                        {
                            scheduleStateItem.BiblePublicationCategoryName = categoryName;
                            logger.Debug("Populated BiblePublicationCategoryName={CategoryName} from publication code for schedule {ScheduleId}",
                                categoryName, scheduleStateItem.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating BiblePublicationName and Category");
            }
        }

        // Section name - for non-sectioned publications (SectionCode is null or empty), clear the section name
        var sectionNumber = await ConvertSectionCodeToIntAsync(
            biblePublicationSchedule.SectionCode,
            biblePublicationSchedule.LanguageCode,
            biblePublicationSchedule.PublicationCode);
        
        if (sectionNumber == 0)
        {
            // Non-sectioned publication - clear section name
            scheduleStateItem.BiblePublicationSectionName = null;
        }
        else if (sectionNumber > 0 &&
            !string.IsNullOrWhiteSpace(biblePublicationSchedule.LanguageCode) &&
            !string.IsNullOrWhiteSpace(biblePublicationSchedule.PublicationCode))
        {
            try
            {
                var biblePublicationSectionService = serviceProvider.GetRequiredService<IBiblePublicationSectionService>();
                var sectionName = await Task.Run(async () =>
                    await biblePublicationSectionService.GetSectionNameAsync(
                        biblePublicationSchedule.LanguageCode,
                        biblePublicationSchedule.PublicationCode,
                        sectionNumber));

                if (!string.IsNullOrWhiteSpace(sectionName))
                {
                    scheduleStateItem.BiblePublicationSectionName = sectionName;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating BiblePublicationSectionName");
            }
        }

        // Track title
        if (biblePublicationSchedule.TrackNumber > 0 &&
            !string.IsNullOrWhiteSpace(biblePublicationSchedule.LanguageCode) &&
            !string.IsNullOrWhiteSpace(biblePublicationSchedule.PublicationCode))
        {
            try
            {
                if (PublicationTypeHelper.HasSectionStructure(biblePublicationSchedule.PublicationCode))
                {
                    // Sectioned publications (traditional Bible) - load track from section
                    var sectionNum = await ConvertSectionCodeToIntAsync(
                        biblePublicationSchedule.SectionCode,
                        biblePublicationSchedule.LanguageCode,
                        biblePublicationSchedule.PublicationCode);
                    if (sectionNum > 0)
                    {
                        var tracks = await Task.Run(async () =>
                            await mediaService.GetBiblePublicationTracks(
                                biblePublicationSchedule.LanguageCode,
                                biblePublicationSchedule.PublicationCode,
                                sectionNum));

                        if (tracks != null && tracks.TryGetValue(biblePublicationSchedule.TrackNumber, out var track))
                        {
                            if (!string.IsNullOrWhiteSpace(track.Title))
                            {
                                scheduleStateItem.BiblePublicationTrackTitle = track.Title;
                            }
                        }
                    }
                }
                else if (BiblePublicationService != null)
                {
                    // Non-sectioned publications (drama/video) - load track directly from publication
                    var publication = await Task.Run(async () =>
                        await BiblePublicationService.GetByLanguageAndCodeWithTracksAsync(
                            biblePublicationSchedule.LanguageCode,
                            biblePublicationSchedule.PublicationCode));

                    if (publication != null)
                    {
                        var track = publication.Tracks.FirstOrDefault(t => t.Number == biblePublicationSchedule.TrackNumber);
                        if (track != null && !string.IsNullOrWhiteSpace(track.Title))
                        {
                            scheduleStateItem.BiblePublicationTrackTitle = track.Title;
                        }

                        // Populate category from publication (for non-sectioned publications that weren't loaded earlier)
                        if (publication.Category != null && 
                            (scheduleStateItem.BiblePublicationCategoryId == null || string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationCategoryName)))
                        {
                            scheduleStateItem.BiblePublicationCategoryId = publication.CategoryId;
                            scheduleStateItem.BiblePublicationCategoryName = publication.Category.CategoryName;
                            logger.Debug("Populated BiblePublicationCategoryId={CategoryId}, BiblePublicationCategoryName={CategoryName} for non-sectioned publication in schedule {ScheduleId}",
                                publication.CategoryId, publication.Category.CategoryName, scheduleStateItem.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating BiblePublicationTrackTitle");
            }
        }
    }

    private async Task PopulateMusicDisplayNamesAsync(ScheduleStateItem scheduleStateItem, AlarmMusic music)
    {
        // Music language name and direction (for vocals)
        if (music.MusicType == MusicType.VocalMusic &&
            !string.IsNullOrWhiteSpace(music.LanguageCode))
        {
            try
            {
                var languagesDict = await Task.Run(async () =>
                    await mediaService.GetVocalMusicLanguages());
                if (languagesDict.TryGetValue(music.LanguageCode, out var language))
                {
                    scheduleStateItem.MusicLanguageName = language.Name;
                    scheduleStateItem.MusicLanguageDirection = language.Direction;
                }
                else
                {
                    scheduleStateItem.MusicLanguageName = music.LanguageCode;
                    scheduleStateItem.MusicLanguageDirection = "ltr"; // Default to LTR if language not found
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating MusicLanguageName");
                scheduleStateItem.MusicLanguageDirection = "ltr"; // Default to LTR on error
            }
        }

        // Music publication name (for vocals and melodies)
        if (!string.IsNullOrWhiteSpace(music.PublicationCode))
        {
            try
            {
                if (music.MusicType == MusicType.VocalMusic &&
                    !string.IsNullOrWhiteSpace(music.LanguageCode))
                {
                    // Populate for vocals
                    var releases = await Task.Run(async () =>
                        await mediaService.GetVocalMusicReleases(music.LanguageCode));
                    if (releases.TryGetValue(music.PublicationCode, out var release))
                    {
                        scheduleStateItem.MusicPublicationName = release.Name;
                    }
                }
                else if (music.MusicType == MusicType.Music)
                {
                    // Populate for melodies (instrumental music)
                    var releases = await Task.Run(async () =>
                        await mediaService.GetMelodyMusicReleases());
                    if (releases.TryGetValue(music.PublicationCode, out var melodyRelease))
                    {
                        scheduleStateItem.MusicPublicationName = melodyRelease.Name;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating MusicPublicationName");
            }
        }

        // Music section name (for publications with sections, e.g., "iam" Kingdom Melodies)
        if (!string.IsNullOrWhiteSpace(music.SectionCode) &&
            !string.IsNullOrWhiteSpace(music.PublicationCode))
        {
            try
            {
                if (music.MusicType == MusicType.VocalMusic)
                {
                    // For vocal music, we need language code and section number
                    if (!string.IsNullOrWhiteSpace(music.LanguageCode))
                    {
                        var sectionNumber = await ConvertSectionCodeToIntAsync(
                            music.SectionCode,
                            music.LanguageCode,
                            music.PublicationCode);

                        if (sectionNumber > 0)
                        {
                            var biblePublicationSectionService = serviceProvider.GetRequiredService<IBiblePublicationSectionService>();
                            var sectionName = await Task.Run(async () =>
                                await biblePublicationSectionService.GetSectionNameAsync(
                                    music.LanguageCode,
                                    music.PublicationCode,
                                    sectionNumber));

                            if (!string.IsNullOrWhiteSpace(sectionName))
                            {
                                scheduleStateItem.MusicSectionName = sectionName;
                            }
                        }
                    }
                }
                else if (music.MusicType == MusicType.Music)
                {
                    // For melodies, music publications are BiblePublications with Category=Music and LanguageId=null
                    // Section codes are strings like "iam-1", "iam-2" - query directly by SectionCode (no need to convert to int)
                    var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
                    using var scope = scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

                    // EF Core can't translate StringComparison.OrdinalIgnoreCase, so we need to load and filter in memory
                    // or use ToLower() in the query. Using ToLower() is more efficient.
                    var sectionCodeLower = music.SectionCode?.ToLowerInvariant();
                    var section = await Task.Run(async () =>
                    {
                        var sections = await dbContext.BiblePublicationSections
                            .AsNoTracking()
                            .Include(x => x.BiblePublication)
                                .ThenInclude(x => x.Category)
                            .Where(x => x.BiblePublication.PublicationCode == music.PublicationCode
                                && x.BiblePublication.Category.CategoryName == "Music"
                                && x.BiblePublication.LanguageId == null
                                && x.SectionCode != null)
                            .ToListAsync();

                        // Filter in memory for case-insensitive comparison
                        var matchingSection = sections.FirstOrDefault(x => 
                            x.SectionCode != null && 
                            x.SectionCode.Equals(music.SectionCode, StringComparison.OrdinalIgnoreCase));

                        return matchingSection?.Name;
                    });

                    if (!string.IsNullOrWhiteSpace(section))
                    {
                        scheduleStateItem.MusicSectionName = section;
                        logger.Debug("Populated MusicSectionName '{MusicSectionName}' for publication {PublicationCode}, section {SectionCode}",
                            section, music.PublicationCode, music.SectionCode);
                    }
                    else
                    {
                        logger.Warning("Music section name not found for publication {PublicationCode}, section {SectionCode}",
                            music.PublicationCode, music.SectionCode);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating MusicSectionName for publication {PublicationCode}, section {SectionCode}",
                    music.PublicationCode, music.SectionCode);
            }
        }

        // Music track name
        if (music.TrackNumber > 0)
        {
            try
            {
                string? trackName = null;
                if (music.MusicType == MusicType.Music)
                {
                    if (!string.IsNullOrWhiteSpace(music.PublicationCode))
                    {
                        var tracks = await Task.Run(async () =>
                            await mediaService.GetMelodyMusicTracks(music.PublicationCode));
                        if (tracks.TryGetValue(music.TrackNumber, out var track))
                        {
                            trackName = $"Melody Number(s) {track.Title}";
                        }
                    }
                }
                else if (music.MusicType == MusicType.VocalMusic)
                {
                    if (!string.IsNullOrWhiteSpace(music.LanguageCode) && !string.IsNullOrWhiteSpace(music.PublicationCode))
                    {
                        var tracks = await Task.Run(async () =>
                            await mediaService.GetVocalMusicTracks(music.LanguageCode, music.PublicationCode));
                        if (tracks.TryGetValue(music.TrackNumber, out var track))
                        {
                            trackName = track.Title;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(trackName))
                {
                    scheduleStateItem.MusicTrackName = trackName;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating MusicTrackName");
            }
        }
    }

    /// <summary>
    /// Converts SectionCode (string) to the int section number needed for media service calls.
    /// Tries to parse SectionCode to int.
    /// Returns 0 for null/empty (non-sectioned publications).
    /// </summary>
    private async Task<int> ConvertSectionCodeToIntAsync(string? sectionCode, string languageCode, string publicationCode)
    {
        if (string.IsNullOrEmpty(sectionCode))
        {
            return 0;
        }

        // Try to parse SectionCode directly to int
        if (int.TryParse(sectionCode, out var sectionNumber))
        {
            return sectionNumber;
        }

        // If parsing fails, SectionCode is not numeric (e.g., "gen" for Genesis)
        // For non-numeric section codes, return 0
        return 0;
    }
}

