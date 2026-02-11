#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Services.Schedule.ScheduleDisplayNameServiceHelpers;

public sealed class ScheduleDisplayNameMusicHelper
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IServiceProvider serviceProvider;

    public ScheduleDisplayNameMusicHelper(ILogger logger, IMediaService mediaService, IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.serviceProvider = serviceProvider;
    }

    public async Task PopulateAsync(ScheduleStateItem scheduleStateItem, AlarmMusic music)
    {
        var isMelodyMusic = string.IsNullOrEmpty(music.LanguageCode);

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
                    scheduleStateItem.MusicLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating MusicLanguageName");
                scheduleStateItem.MusicLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
            }
        }
        else if (isMelodyMusic)
        {
            try
            {
                var languagesDict = await mediaService.GetVocalMusicLanguages();
                if (languagesDict.TryGetValue(AppConstants.Media.DefaultLanguageCode, out var englishLanguage))
                {
                    scheduleStateItem.MusicLanguageName = englishLanguage.Name;
                    scheduleStateItem.MusicLanguageDirection = englishLanguage.Direction ?? AppConstants.Media.TextDirectionLeftToRight;
                }
                else
                {
                    scheduleStateItem.MusicLanguageName = "English";
                    scheduleStateItem.MusicLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating MusicLanguageName for melody");
                scheduleStateItem.MusicLanguageName = "English";
                scheduleStateItem.MusicLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
            }
        }

        if (!string.IsNullOrWhiteSpace(music.PublicationCode))
        {
            try
            {
                if (!isMelodyMusic && !string.IsNullOrWhiteSpace(music.LanguageCode))
                {
                    var vocalMusicService = serviceProvider.GetRequiredService<IVocalMusicService>();
                    var release = await vocalMusicService.GetByLanguageAndCodeAsync(music.LanguageCode, music.PublicationCode);
                    if (release != null)
                        scheduleStateItem.MusicPublicationName = release.Name;
                }
                else if (isMelodyMusic)
                {
                    var releases = await mediaService.GetMelodyMusicReleases();
                    if (releases.TryGetValue(music.PublicationCode, out var melodyRelease))
                        scheduleStateItem.MusicPublicationName = melodyRelease.Name;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating MusicPublicationName");
            }
        }

        if (!string.IsNullOrWhiteSpace(music.SectionCode) && !string.IsNullOrWhiteSpace(music.PublicationCode))
        {
            try
            {
                if (!isMelodyMusic && !string.IsNullOrWhiteSpace(music.LanguageCode))
                {
                    var sectionCode = SectionCodeHelper.Normalize(music.SectionCode);
                    if (!string.IsNullOrWhiteSpace(sectionCode))
                    {
                        var biblePublicationSectionService = serviceProvider.GetRequiredService<IBiblePublicationSectionService>();
                        var sectionName = await Task.Run(async () => await biblePublicationSectionService.GetSectionNameAsync(music.LanguageCode, music.PublicationCode, sectionCode));
                        if (!string.IsNullOrWhiteSpace(sectionName))
                            scheduleStateItem.MusicSectionName = sectionName;
                    }
                }
                else
                {
                    var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
                    using var scope = scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
                    var sectionCodeLower = music.SectionCode.ToLowerInvariant();
                    var section = await dbContext.BiblePublicationSections
                        .AsNoTracking()
                        .Where(x => x.BiblePublication.PublicationCode == music.PublicationCode && x.BiblePublication.Category!.CategoryName == "Music" && x.BiblePublication.LanguageId == null && x.SectionCode != null && x.SectionCode.ToLower() == sectionCodeLower)
                        .Select(x => x.Name)
                        .FirstOrDefaultAsync();
                    if (!string.IsNullOrWhiteSpace(section))
                    {
                        scheduleStateItem.MusicSectionName = section;
                        logger.Debug("Populated MusicSectionName '{MusicSectionName}' for publication {PublicationCode}, section {SectionCode}", section, music.PublicationCode, music.SectionCode);
                    }
                    else
                        logger.Warning("Music section name not found for publication {PublicationCode}, section {SectionCode}", music.PublicationCode, music.SectionCode);
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating MusicSectionName for publication {PublicationCode}, section {SectionCode}", music.PublicationCode, music.SectionCode);
            }
        }

        if (!string.IsNullOrWhiteSpace(music.TrackCode))
        {
            try
            {
                string? trackName = null;
                if (isMelodyMusic && !string.IsNullOrWhiteSpace(music.PublicationCode))
                {
                    SortedDictionary<int, Bible.Alarm.Shared.Models.Media.Music.MusicTrack> tracks;
                    if (PublicationTypeHelper.HasSectionStructure(music.PublicationCode))
                        tracks = string.IsNullOrWhiteSpace(music.SectionCode) ? new SortedDictionary<int, Bible.Alarm.Shared.Models.Media.Music.MusicTrack>() : await Task.Run(async () => await mediaService.GetMelodyMusicTracksBySection(music.PublicationCode, music.SectionCode));
                    else
                        tracks = await Task.Run(async () => await mediaService.GetMelodyMusicTracks(music.PublicationCode));
                    if (!string.IsNullOrWhiteSpace(music.TrackCode) && int.TryParse(music.TrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var trackNum) && tracks.TryGetValue(trackNum, out var track))
                        trackName = DisplayNameNormalizer.NormalizeTrackTitle(track.Title) ?? track.Title;
                }
                else if (!string.IsNullOrWhiteSpace(music.LanguageCode) && !string.IsNullOrWhiteSpace(music.PublicationCode))
                {
                    var tracks = await Task.Run(async () => await mediaService.GetVocalMusicTracks(music.LanguageCode, music.PublicationCode));
                    if (!string.IsNullOrWhiteSpace(music.TrackCode) && int.TryParse(music.TrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var trackNum) && tracks.TryGetValue(trackNum, out var track))
                        trackName = track.Title;
                }
                if (!string.IsNullOrWhiteSpace(trackName))
                    scheduleStateItem.MusicTrackName = trackName;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error populating MusicTrackName");
            }
        }
    }
}
