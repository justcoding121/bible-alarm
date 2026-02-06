#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Net;

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

                        // No-language publications (e.g. "iam") store LanguageCode "E" as the effective code for playback.
                        // Keep the already-resolved language display name (e.g. "English") so the UI shows "English"
                        // instead of "E" in the bible publication container.
                        if (publicationWithoutLanguage)
                        {
                            logger.Debug("No-language publication {PublicationCode}; keeping BiblePublicationLanguageName for display (LanguageCode: {LanguageCode})",
                                publicationCode, scheduleLanguageCode ?? "null");
                        }
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
                        !string.IsNullOrWhiteSpace(biblePublicationSchedule.TrackCode) &&
                        publication.Tracks != null &&
                        publication.Tracks.Count > 0)
                    {
                        var track = publication.Tracks.FirstOrDefault(t => t.TrackCode == biblePublicationSchedule.TrackCode);
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

            // Final fallbacks: never leave the editor blank if we at least have codes.
            if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationName))
            {
                scheduleStateItem.BiblePublicationName = publicationCode;
            }

            if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationCategoryName))
            {
                var categoryName = JwSourceHelper.GetCategoryName(publicationCode);
                if (!string.IsNullOrWhiteSpace(categoryName))
                {
                    scheduleStateItem.BiblePublicationCategoryName = categoryName;
                }
            }
        }

        // Section name - for non-sectioned publications (SectionCode is null or empty), clear the section name
        var normalizedSectionCode = SectionCodeHelper.Normalize(biblePublicationSchedule.SectionCode);
        if (string.IsNullOrWhiteSpace(normalizedSectionCode))
        {
            // Non-sectioned publication - clear section name
            scheduleStateItem.BiblePublicationSectionName = null;
        }
        else if (!string.IsNullOrWhiteSpace(publicationCode))
        {
            try
            {
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
                            // IMPORTANT:
                            // Do NOT return here. For no-language sectioned publications (e.g. "iam"),
                            // we still need to populate the track title (melody numbers) below.
                            // We only want to skip *other section-name* strategies.
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Debug(ex, "No-language section lookup failed (PublicationCode={PublicationCode}, SectionCode={SectionCode})", publicationCode, normalizedSectionCode);
                    }
                }

                // Fallback: language-bound section lookup (traditional Bible section structure).
                if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationSectionName) &&
                    !string.IsNullOrWhiteSpace(scheduleLanguageCode))
                {
                    var biblePublicationSectionService = serviceProvider.GetRequiredService<IBiblePublicationSectionService>();
                    var sectionName = await Task.Run(async () =>
                        await biblePublicationSectionService.GetSectionNameAsync(
                            scheduleLanguageCode,
                            publicationCode,
                            normalizedSectionCode));

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
        if (!string.IsNullOrWhiteSpace(biblePublicationSchedule.TrackCode) &&
            !string.IsNullOrWhiteSpace(publicationCode))
        {
            try
            {
                if (PublicationTypeHelper.HasSectionStructure(publicationCode))
                {
                    // Sectioned publications (traditional Bible) - load track from section
                    var sectionCodeForTracks = SectionCodeHelper.Normalize(biblePublicationSchedule.SectionCode);
                    if (!string.IsNullOrWhiteSpace(sectionCodeForTracks))
                    {
                        var categoryName =
                            scheduleStateItem.BiblePublicationCategoryName
                            ?? JwSourceHelper.GetCategoryName(publicationCode)
                            ?? string.Empty;

                        // For Music-category schedules (e.g. "iam"), prefer MelodyMusicService tracks
                        // so we show the melody numbers instead of a meaningless track index.
                        if (string.Equals(categoryName, "Music", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var melodyTracks = await mediaService.GetMelodyMusicTracksBySection(publicationCode, sectionCodeForTracks);
                                if (!string.IsNullOrWhiteSpace(biblePublicationSchedule.TrackCode) &&
                                    int.TryParse(biblePublicationSchedule.TrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var melodyTrackNum) &&
                                    melodyTracks.TryGetValue(melodyTrackNum, out var melodyTrack) && melodyTrack != null)
                                {
                                    var normalized = NormalizeTrackTitle(melodyTrack.Title);
                                    if (!string.IsNullOrWhiteSpace(normalized))
                                    {
                                        scheduleStateItem.BiblePublicationTrackTitle = normalized;
                                        return;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Debug(ex, "Failed to resolve melody track title (PublicationCode={PublicationCode}, SectionCode={SectionCode}, TrackCode={TrackCode})",
                                    publicationCode, sectionCodeForTracks, biblePublicationSchedule.TrackCode);
                            }
                        }

                        // Try language-bound first; if empty (no-language publications like "iam"), fall back to empty language.
                        var languageForTracks = scheduleLanguageCode ?? string.Empty;
                        var tracks = await Task.Run(async () =>
                            await mediaService.GetBiblePublicationTracks(
                                languageForTracks,
                                publicationCode,
                                sectionCodeForTracks));

                        if (tracks == null || tracks.Count == 0)
                        {
                            tracks = await Task.Run(async () =>
                                await mediaService.GetBiblePublicationTracks(
                                    string.Empty,
                                    publicationCode,
                                    sectionCodeForTracks));
                        }

                        if (tracks != null && !string.IsNullOrWhiteSpace(biblePublicationSchedule.TrackCode) &&
                            tracks.TryGetValue(biblePublicationSchedule.TrackCode, out var track))
                        {
                            if (!string.IsNullOrWhiteSpace(track.Title))
                            {
                                scheduleStateItem.BiblePublicationTrackTitle = NormalizeTrackTitle(track.Title) ?? track.Title;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating BiblePublicationTrackTitle");
            }

            // Fallback: show a stable label even if track title lookup fails.
            if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationTrackTitle))
            {
                var categoryName =
                    scheduleStateItem.BiblePublicationCategoryName
                    ?? JwSourceHelper.GetCategoryName(publicationCode)
                    ?? string.Empty;

                // Never label Music-category schedules as "Chapter".
                scheduleStateItem.BiblePublicationTrackTitle =
                    string.Equals(categoryName, "Music", StringComparison.OrdinalIgnoreCase)
                        ? $"Track {biblePublicationSchedule.TrackCode}"
                        : PublicationTypeHelper.HasSectionStructure(publicationCode)
                            ? $"Chapter {biblePublicationSchedule.TrackCode}"
                            : $"Track {biblePublicationSchedule.TrackCode}";
            }
        }
    }

    private static string? NormalizeTrackTitle(string? rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            return null;
        }

        var decoded = WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ').Trim();
        if (decoded.Length == 0)
        {
            return null;
        }

        // IMPORTANT: For melody discs (e.g. "iam"), the harvester owns the final title string.
        // The app should not add any prefixes or reformatting here.
        return decoded;
    }

    private async Task PopulateMusicDisplayNamesAsync(ScheduleStateItem scheduleStateItem, AlarmMusic music)
    {
        // Music type is inferred from LanguageCode: NULL/empty = instrumental (melody), otherwise = vocal
        var isMelodyMusic = string.IsNullOrEmpty(music.LanguageCode);

        // Music language name and direction.
        // For vocal: use selected language. For melody (no-language): show "English" like Bible container for no-language pubs.
        if (!isMelodyMusic && !string.IsNullOrWhiteSpace(music.LanguageCode))
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
                    scheduleStateItem.MusicLanguageDirection = "ltr";
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating MusicLanguageName");
                scheduleStateItem.MusicLanguageDirection = "ltr";
            }
        }
        else if (isMelodyMusic)
        {
            // Melody (no-language): set display to "English" so row shows "English"; publication modal uses "E" and shows English + no-language pubs (same pattern as Bible).
            try
            {
                var languagesDict = await mediaService.GetVocalMusicLanguages();
                if (languagesDict.TryGetValue("E", out var englishLanguage))
                {
                    scheduleStateItem.MusicLanguageName = englishLanguage.Name;
                    scheduleStateItem.MusicLanguageDirection = englishLanguage.Direction ?? "ltr";
                }
                else
                {
                    scheduleStateItem.MusicLanguageName = "English";
                    scheduleStateItem.MusicLanguageDirection = "ltr";
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating MusicLanguageName for melody");
                scheduleStateItem.MusicLanguageName = "English";
                scheduleStateItem.MusicLanguageDirection = "ltr";
            }
        }

        // Music publication name (for vocals and melodies)
        if (!string.IsNullOrWhiteSpace(music.PublicationCode))
        {
            try
            {
                if (!isMelodyMusic && !string.IsNullOrWhiteSpace(music.LanguageCode))
                {
                    // Populate for vocals
                    var vocalMusicService = serviceProvider.GetRequiredService<IVocalMusicService>();
                    var release = await vocalMusicService.GetByLanguageAndCodeAsync(music.LanguageCode, music.PublicationCode);
                    if (release != null)
                    {
                        scheduleStateItem.MusicPublicationName = release.Name;
                    }
                }
                else if (isMelodyMusic)
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
                if (!isMelodyMusic)
                {
                    // For vocal music, we need language code and section number
                    if (!string.IsNullOrWhiteSpace(music.LanguageCode))
                    {
                        var sectionCode = SectionCodeHelper.Normalize(music.SectionCode);
                        if (!string.IsNullOrWhiteSpace(sectionCode))
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
                else
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
        if (!string.IsNullOrWhiteSpace(music.TrackCode))
        {
            try
            {
                string? trackName = null;
                if (isMelodyMusic)
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
                        if (!string.IsNullOrWhiteSpace(music.TrackCode) &&
                            int.TryParse(music.TrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var trackNum) &&
                            tracks.TryGetValue(trackNum, out var track))
                        {
                            trackName = NormalizeTrackTitle(track.Title) ?? track.Title;
                        }
                    }
                }
                else
                {
                    // Vocal music
                    if (!string.IsNullOrWhiteSpace(music.LanguageCode) && !string.IsNullOrWhiteSpace(music.PublicationCode))
                    {
                        var tracks = await Task.Run(async () =>
                            await mediaService.GetVocalMusicTracks(music.LanguageCode, music.PublicationCode));
                        if (!string.IsNullOrWhiteSpace(music.TrackCode) &&
                            int.TryParse(music.TrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var trackNum) &&
                            tracks.TryGetValue(trackNum, out var track))
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
}

