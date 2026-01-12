#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
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

        // Publication name
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

                if (publication != null && !string.IsNullOrWhiteSpace(publication.Name))
                {
                    scheduleStateItem.BiblePublicationName = publication.Name;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating BiblePublicationName");
            }
        }

        // Section name - for non-sectioned publications (SectionNumber is null or 0), clear the section name
        if (!biblePublicationSchedule.SectionNumber.HasValue || biblePublicationSchedule.SectionNumber == 0)
        {
            // Non-sectioned publication - clear section name
            scheduleStateItem.BiblePublicationSectionName = null;
        }
        else if (biblePublicationSchedule.SectionNumber > 0 &&
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
                        biblePublicationSchedule.SectionNumber.Value));

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
                    if (biblePublicationSchedule.SectionNumber.HasValue)
                    {
                        var tracks = await Task.Run(async () =>
                            await mediaService.GetBiblePublicationTracks(
                                biblePublicationSchedule.LanguageCode,
                                biblePublicationSchedule.PublicationCode,
                                biblePublicationSchedule.SectionNumber.Value));

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
        if (music.MusicType == MusicType.Vocals &&
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

        // Music publication name (for vocals)
        if (music.MusicType == MusicType.Vocals &&
            !string.IsNullOrWhiteSpace(music.LanguageCode) &&
            !string.IsNullOrWhiteSpace(music.PublicationCode))
        {
            try
            {
                var releases = await Task.Run(async () =>
                    await mediaService.GetVocalMusicReleases(music.LanguageCode));
                if (releases.TryGetValue(music.PublicationCode, out var release))
                {
                    scheduleStateItem.MusicPublicationName = release.Name;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating MusicPublicationName");
            }
        }

        // Music track name
        if (music.TrackNumber > 0)
        {
            try
            {
                string? trackName = null;
                if (music.MusicType == MusicType.Melodies)
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
                else if (music.MusicType == MusicType.Vocals)
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
}

