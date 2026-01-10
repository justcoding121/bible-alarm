#nullable enable
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
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
        if (schedule.BibleReadingSchedule != null)
        {
            await PopulateBibleReadingDisplayNamesAsync(scheduleStateItem, schedule.BibleReadingSchedule);
        }

        // Populate music display names
        if (schedule.Music != null)
        {
            await PopulateMusicDisplayNamesAsync(scheduleStateItem, schedule.Music);
        }
    }

    private async Task PopulateBibleReadingDisplayNamesAsync(ScheduleStateItem scheduleStateItem, BibleReadingSchedule bibleReadingSchedule)
    {
        // Language name
        if (!string.IsNullOrWhiteSpace(bibleReadingSchedule.LanguageCode) && BiblePublicationService != null)
        {
            try
            {
                var languagesDict = await Task.Run(async () =>
                    await BiblePublicationService.GetDistinctLanguagesAsync());
                if (languagesDict.TryGetValue(bibleReadingSchedule.LanguageCode, out var language))
                {
                    scheduleStateItem.BibleReadingLanguageName = language.Name;
                }
                else
                {
                    scheduleStateItem.BibleReadingLanguageName = bibleReadingSchedule.LanguageCode;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating BibleReadingLanguageName");
                scheduleStateItem.BibleReadingLanguageName = bibleReadingSchedule.LanguageCode;
            }
        }

        // Translation name
        if (!string.IsNullOrWhiteSpace(bibleReadingSchedule.LanguageCode) &&
            !string.IsNullOrWhiteSpace(bibleReadingSchedule.PublicationCode) &&
            BiblePublicationService != null)
        {
            try
            {
                var translation = await Task.Run(async () =>
                    await BiblePublicationService.GetByLanguageAndCodeWithBooksAsync(
                        bibleReadingSchedule.LanguageCode,
                        bibleReadingSchedule.PublicationCode));

                if (translation != null && !string.IsNullOrWhiteSpace(translation.Name))
                {
                    scheduleStateItem.BibleReadingPublicationName = translation.Name;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating BibleReadingPublicationName");
            }
        }

        // Book name
        if (bibleReadingSchedule.BookNumber > 0 &&
            !string.IsNullOrWhiteSpace(bibleReadingSchedule.LanguageCode) &&
            !string.IsNullOrWhiteSpace(bibleReadingSchedule.PublicationCode))
        {
            try
            {
                var bibleBookService = serviceProvider.GetRequiredService<IBibleBookService>();
                if (!bibleReadingSchedule.BookNumber.HasValue) return;
                var bookName = await Task.Run(async () =>
                    await bibleBookService.GetBookNameAsync(
                        bibleReadingSchedule.LanguageCode,
                        bibleReadingSchedule.PublicationCode,
                        bibleReadingSchedule.BookNumber.Value));

                if (!string.IsNullOrWhiteSpace(bookName))
                {
                    scheduleStateItem.BibleReadingBookName = bookName;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating BibleReadingBookName");
            }
        }
    }

    private async Task PopulateMusicDisplayNamesAsync(ScheduleStateItem scheduleStateItem, AlarmMusic music)
    {
        // Music language name (for vocals)
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
                }
                else
                {
                    scheduleStateItem.MusicLanguageName = music.LanguageCode;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating MusicLanguageName");
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

