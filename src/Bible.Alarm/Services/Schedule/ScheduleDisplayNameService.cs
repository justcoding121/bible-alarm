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
        var scheduleLanguageCode = biblePublicationSchedule.LanguageCode;
        var publicationCode = biblePublicationSchedule.PublicationCode;

        // Language name and direction
        if (!string.IsNullOrWhiteSpace(scheduleLanguageCode) && BiblePublicationService != null)
        {
            try
            {
                var languagesDict = await BiblePublicationService.GetDistinctLanguagesAsync();
                if (languagesDict.TryGetValue(scheduleLanguageCode, out var language))
                {
                    scheduleStateItem.BiblePublicationLanguageName = language.Name;
                    scheduleStateItem.BiblePublicationLanguageDirection = language.Direction;
                }
                else
                {
                    scheduleStateItem.BiblePublicationLanguageName = scheduleLanguageCode;
                    scheduleStateItem.BiblePublicationLanguageDirection = "ltr"; // Default to LTR
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating BiblePublicationLanguageName");
                scheduleStateItem.BiblePublicationLanguageName = scheduleLanguageCode;
                scheduleStateItem.BiblePublicationLanguageDirection = "ltr"; // Default to LTR
            }
        }

        // Publication name and category
        if (!string.IsNullOrWhiteSpace(publicationCode))
        {
            try
            {
                var hasSections = PublicationTypeHelper.HasSectionStructure(publicationCode);
                Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublication? publication = null;
                var publicationWithoutLanguage = false;

                // First attempt: language-bound query (normal publications).
                if (!string.IsNullOrWhiteSpace(scheduleLanguageCode) && BiblePublicationService != null)
                {
                    // Avoid redundant DB calls:
                    // - For sectioned publications, load with sections once.
                    // - For non-sectioned publications (dramas/videos), load with tracks once and reuse for track title later.
                    publication = hasSections
                        ? await BiblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
                            scheduleLanguageCode,
                            publicationCode)
                        : await BiblePublicationService.GetByLanguageAndCodeWithTracksAsync(
                            scheduleLanguageCode,
                            publicationCode);
                }

                // Fallback: publications without language FK (e.g., melody music like "iam").
                if (publication == null)
                {
                    try
                    {
                        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
                        using var scope = scopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

                        publication = await dbContext.BiblePublications
                            .AsNoTracking()
                            .Include(x => x.Category)
                            .Where(x => x.PublicationCode == publicationCode && x.LanguageId == null)
                            .FirstOrDefaultAsync();

                        publicationWithoutLanguage = publication != null;
                    }
                    catch (Exception ex)
                    {
                        logger.Warning(ex, "Error loading publication without language FK from media index (PublicationCode={PublicationCode})", publicationCode);
                    }
                }

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
                        var categoryName = JwSourceHelper.GetCategoryName(publicationCode);
                        if (!string.IsNullOrWhiteSpace(categoryName))
                        {
                            scheduleStateItem.BiblePublicationCategoryName = categoryName;
                            logger.Debug("Populated BiblePublicationCategoryName={CategoryName} from publication code for schedule {ScheduleId}",
                                categoryName, scheduleStateItem.Id);
                        }
                    }

                    // For non-sectioned publications, we already loaded tracks above: populate track title here
                    // so we don't need a second call to GetByLanguageAndCodeWithTracksAsync later.
                    if (!hasSections &&
                        biblePublicationSchedule.TrackNumber > 0 &&
                        publication.Tracks != null &&
                        publication.Tracks.Count > 0)
                    {
                        var track = publication.Tracks.FirstOrDefault(t => t.Number == biblePublicationSchedule.TrackNumber);
                        if (track != null && !string.IsNullOrWhiteSpace(track.Title))
                        {
                            scheduleStateItem.BiblePublicationTrackTitle = track.Title;
                        }
                    }

                    // For no-language publications, the schedule category is not persisted in Schedule DB.
                    // Ensure category is set based on media index so the schedule editor can render correctly.
                    if (publicationWithoutLanguage && string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationCategoryName))
                    {
                        var categoryName = JwSourceHelper.GetCategoryName(publicationCode);
                        if (!string.IsNullOrWhiteSpace(categoryName))
                        {
                            scheduleStateItem.BiblePublicationCategoryName = categoryName;
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
        var sectionCode = await ConvertSectionCodeToIntAsync(
            biblePublicationSchedule.SectionCode,
            scheduleLanguageCode ?? string.Empty,
            publicationCode ?? string.Empty);
        
        if (sectionCode == 0)
        {
            // Non-sectioned publication - clear section name
            scheduleStateItem.BiblePublicationSectionName = null;
        }
        else if (sectionCode > 0 &&
            !string.IsNullOrWhiteSpace(publicationCode))
        {
            try
            {
                var normalizedSectionCode = SectionCodeHelper.Normalize(biblePublicationSchedule.SectionCode);

                // First try: no-language section lookup by exact SectionCode (e.g. "iam-1").
                // This is required for melody disc-style publications where LanguageId == null in media index.
                if (!string.IsNullOrWhiteSpace(normalizedSectionCode))
                {
                    try
                    {
                        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
                        using var scope = scopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

                        var sectionCodeLower = normalizedSectionCode.ToLowerInvariant();
                        var sectionName = await dbContext.BiblePublicationSections
                            .AsNoTracking()
                            .Where(x => x.BiblePublication.PublicationCode == publicationCode
                                        && x.BiblePublication.LanguageId == null
                                        && x.SectionCode != null
                                        && x.SectionCode.ToLower() == sectionCodeLower)
                            .Select(x => x.Name)
                            .FirstOrDefaultAsync();

                        if (!string.IsNullOrWhiteSpace(sectionName))
                        {
                            scheduleStateItem.BiblePublicationSectionName = sectionName;
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Debug(ex, "No-language section lookup failed (PublicationCode={PublicationCode}, SectionCode={SectionCode})", publicationCode, normalizedSectionCode);
                    }
                }

                // Fallback: language-bound section lookup (traditional Bible section structure).
                if (!string.IsNullOrWhiteSpace(scheduleLanguageCode))
                {
                    var biblePublicationSectionService = serviceProvider.GetRequiredService<IBiblePublicationSectionService>();
                    var sectionName = await Task.Run(async () =>
                        await biblePublicationSectionService.GetSectionNameAsync(
                            scheduleLanguageCode,
                            publicationCode,
                            sectionCode));

                    if (!string.IsNullOrWhiteSpace(sectionName))
                    {
                        scheduleStateItem.BiblePublicationSectionName = sectionName;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating BiblePublicationSectionName");
            }
        }

        // Track title
        if (biblePublicationSchedule.TrackNumber > 0 &&
            !string.IsNullOrWhiteSpace(publicationCode))
        {
            try
            {
                if (PublicationTypeHelper.HasSectionStructure(publicationCode))
                {
                    // Sectioned publications (traditional Bible) - load track from section
                    var sectionIndex = await ConvertSectionCodeToIntAsync(
                        biblePublicationSchedule.SectionCode,
                        scheduleLanguageCode ?? string.Empty,
                        publicationCode);
                    if (sectionIndex > 0)
                    {
                        // Try language-bound first; if empty (no-language publications like "iam"), fall back to empty language.
                        var languageForTracks = scheduleLanguageCode ?? string.Empty;
                        var tracks = await Task.Run(async () =>
                            await mediaService.GetBiblePublicationTracks(
                                languageForTracks,
                                publicationCode,
                                sectionIndex));

                        if (tracks == null || tracks.Count == 0)
                        {
                            tracks = await Task.Run(async () =>
                                await mediaService.GetBiblePublicationTracks(
                                    string.Empty,
                                    publicationCode,
                                    sectionIndex));
                        }

                        if (tracks != null && tracks.TryGetValue(biblePublicationSchedule.TrackNumber, out var track))
                        {
                            if (!string.IsNullOrWhiteSpace(track.Title))
                            {
                                scheduleStateItem.BiblePublicationTrackTitle = track.Title;
                            }
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
                var languagesDict = await mediaService.GetVocalMusicLanguages();
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
                    var vocalMusicService = serviceProvider.GetRequiredService<IVocalMusicService>();
                    var release = await vocalMusicService.GetByLanguageAndCodeAsync(music.LanguageCode, music.PublicationCode);
                    if (release != null)
                    {
                        scheduleStateItem.MusicPublicationName = release.Name;
                    }
                }
                else if (music.MusicType == MusicType.Music)
                {
                    // Populate for melodies (instrumental music)
                    var releases = await mediaService.GetMelodyMusicReleases();
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
                        var sectionCode = await ConvertSectionCodeToIntAsync(
                            music.SectionCode,
                            music.LanguageCode,
                            music.PublicationCode);

                        if (sectionCode > 0)
                        {
                            var biblePublicationSectionService = serviceProvider.GetRequiredService<IBiblePublicationSectionService>();
                            var sectionName = await Task.Run(async () =>
                                await biblePublicationSectionService.GetSectionNameAsync(
                                    music.LanguageCode,
                                    music.PublicationCode,
                                    sectionCode));

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

                    // Use ToLower() for case-insensitive matching (translatable by EF Core).
                    var sectionCodeLower = music.SectionCode.ToLowerInvariant();
                    var section = await dbContext.BiblePublicationSections
                        .AsNoTracking()
                        .Where(x => x.BiblePublication.PublicationCode == music.PublicationCode
                                    && x.BiblePublication.Category.CategoryName == "Music"
                                    && x.BiblePublication.LanguageId == null
                                    && x.SectionCode != null
                                    && x.SectionCode.ToLower() == sectionCodeLower)
                        .Select(x => x.Name)
                        .FirstOrDefaultAsync();

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
                        // IMPORTANT: Sectioned melody publications (e.g., "iam") have duplicate track numbers across discs.
                        // Always resolve track title from the selected disc (SectionCode) when sectioned.
                        SortedDictionary<int, Bible.Alarm.Shared.Models.Media.Music.MusicTrack> tracks;
                        if (PublicationTypeHelper.HasSectionStructure(music.PublicationCode))
                        {
                            if (string.IsNullOrWhiteSpace(music.SectionCode))
                            {
                                tracks = new SortedDictionary<int, Bible.Alarm.Shared.Models.Media.Music.MusicTrack>();
                            }
                            else
                            {
                                tracks = await Task.Run(async () =>
                                    await mediaService.GetMelodyMusicTracksBySection(music.PublicationCode, music.SectionCode));
                            }
                        }
                        else
                        {
                            tracks = await Task.Run(async () =>
                                await mediaService.GetMelodyMusicTracks(music.PublicationCode));
                        }
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
    /// Returns 0 for null/empty (non-sectioned publications).
    /// Supports codes like "iam-1" by extracting the numeric suffix.
    /// </summary>
    private Task<int> ConvertSectionCodeToIntAsync(string? sectionCode, string languageCode, string publicationCode)
    {
        return Task.FromResult(SectionCodeHelper.GetSectionIndexOrZero(sectionCode));
    }
}

