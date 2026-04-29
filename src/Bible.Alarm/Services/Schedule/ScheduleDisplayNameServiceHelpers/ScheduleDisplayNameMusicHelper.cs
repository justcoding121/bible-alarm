#nullable enable

using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Services.Schedule.ScheduleDisplayNameServiceHelpers;

public sealed class ScheduleDisplayNameMusicHelper
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly ILanguageNameService languageNameService;
    private readonly IServiceProvider serviceProvider;

    public ScheduleDisplayNameMusicHelper(ILogger logger, IMediaService mediaService, ILanguageNameService languageNameService, IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.languageNameService = languageNameService;
        this.serviceProvider = serviceProvider;
    }

    public async Task PopulateAsync(ScheduleStateItem scheduleStateItem, AlarmMusic music)
    {
        // Melody = no-language publication (e.g. iam). Use publication list so saved display language (e.g. MY) is preserved on load.
        var melodyReleases = await mediaService.GetMelodyMusicReleases();
        var isMelodyPublication = !string.IsNullOrWhiteSpace(music.PublicationCode) &&
            (melodyReleases.ContainsKey(music.PublicationCode) ||
             melodyReleases.Keys.Any(k => string.Equals(k, music.PublicationCode, StringComparison.OrdinalIgnoreCase)));
        var isMelodyMusic = isMelodyPublication;

        if (isMelodyPublication)
        {
            try
            {
                var languagesDict = await mediaService.GetVocalMusicLanguages();
                var displayLanguageCode = !string.IsNullOrWhiteSpace(music.LanguageCode) ? music.LanguageCode : AppConstants.Media.DefaultLanguageCode;
                if (languagesDict.TryGetValue(displayLanguageCode, out var language))
                {
                    scheduleStateItem.MusicLanguageName = languageNameService.GetNameCached(language.Id) ?? displayLanguageCode;
                    scheduleStateItem.MusicLanguageDirection = language.Direction ?? AppConstants.Media.TextDirectionLeftToRight;
                }
                else
                {
                    scheduleStateItem.MusicLanguageName = string.IsNullOrWhiteSpace(music.LanguageCode) ? AppConstants.Media.DefaultLanguageDisplayNameEnglish : music.LanguageCode;
                    scheduleStateItem.MusicLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, AppConstants.Logging.ScheduleDisplayNameDiagnosticsLog.ErrorPopulatingMusicLanguageNameForMelody);
                scheduleStateItem.MusicLanguageName = string.IsNullOrWhiteSpace(music.LanguageCode) ? AppConstants.Media.DefaultLanguageDisplayNameEnglish : music.LanguageCode;
                scheduleStateItem.MusicLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
            }
        }
        else if (!string.IsNullOrWhiteSpace(music.LanguageCode))
        {
            try
            {
                var languagesDict = await mediaService.GetVocalMusicLanguages();
                if (languagesDict.TryGetValue(music.LanguageCode, out var language))
                {
                    scheduleStateItem.MusicLanguageName = languageNameService.GetNameCached(language.Id) ?? music.LanguageCode;
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
                logger.Warning(ex, AppConstants.Logging.ScheduleDisplayNameDiagnosticsLog.ErrorPopulatingMusicLanguageName);
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
                    var set = releases.TryGetValue(music.PublicationCode, out var melodyRelease);
                    if (!set && !string.IsNullOrEmpty(music.PublicationCode))
                    {
                        var match = releases.FirstOrDefault(kv => string.Equals(kv.Key, music.PublicationCode, StringComparison.OrdinalIgnoreCase));
                        set = match.Key != null;
                        melodyRelease = match.Value;
                    }
                    if (set && melodyRelease != null)
                        scheduleStateItem.MusicPublicationName = melodyRelease.Name;
                    if (string.IsNullOrWhiteSpace(scheduleStateItem.MusicPublicationName))
                        scheduleStateItem.MusicPublicationName = JwSourceHelper.GetPublicationDisplayNameFallback(music.PublicationCode);
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, AppConstants.Logging.ScheduleDisplayNameDiagnosticsLog.ErrorPopulatingMusicPublicationName);
                if (isMelodyMusic && string.IsNullOrWhiteSpace(scheduleStateItem.MusicPublicationName))
                    scheduleStateItem.MusicPublicationName = JwSourceHelper.GetPublicationDisplayNameFallback(music.PublicationCode);
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
                        .Where(x => x.BiblePublication.PublicationCode == music.PublicationCode && x.BiblePublication.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == AppConstants.Media.BiblePublicationCategoryMusic) && x.BiblePublication.LanguageId == null && x.SectionCode != null && x.SectionCode.ToLower() == sectionCodeLower)
                        .Select(x => x.Name)
                        .FirstOrDefaultAsync();
                    if (!string.IsNullOrWhiteSpace(section))
                    {
                        scheduleStateItem.MusicSectionName = section;
                        logger.Debug(AppConstants.Logging.ScheduleDisplayNameDiagnosticsLog.PopulatedMusicSectionNameForPublicationSection,
                            section,
                            music.PublicationCode,
                            music.SectionCode);
                    }
                    else
                    {
                        logger.Warning(AppConstants.Logging.ScheduleDisplayNameDiagnosticsLog.MusicSectionNameNotFoundForPublicationSection, music.PublicationCode, music.SectionCode);
                        if (isMelodyMusic)
                            scheduleStateItem.MusicSectionName = GetMelodySectionDisplayNameFallback(music.PublicationCode, music.SectionCode);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, AppConstants.Logging.ScheduleDisplayNameDiagnosticsLog.ErrorPopulatingMusicSectionNameForPublicationSection, music.PublicationCode, music.SectionCode);
                if (isMelodyMusic && string.IsNullOrWhiteSpace(scheduleStateItem.MusicSectionName))
                    scheduleStateItem.MusicSectionName = GetMelodySectionDisplayNameFallback(music.PublicationCode, music.SectionCode);
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
                    if (!string.IsNullOrWhiteSpace(music.TrackCode) && Bible.Alarm.Shared.Helpers.MusicTrackLookupHelper.TryGetByCode(tracks, music.TrackCode, out var melodyPair))
                        trackName = DisplayNameNormalizer.NormalizeTrackTitle(melodyPair.Track.Title) ?? melodyPair.Track.Title;
                }
                else if (!string.IsNullOrWhiteSpace(music.LanguageCode) && !string.IsNullOrWhiteSpace(music.PublicationCode))
                {
                    var tracks = await Task.Run(async () => await mediaService.GetVocalMusicTracks(music.LanguageCode, music.PublicationCode));
                    if (!string.IsNullOrWhiteSpace(music.TrackCode) && Bible.Alarm.Shared.Helpers.MusicTrackLookupHelper.TryGetByCode(tracks, music.TrackCode, out var vocalPair))
                        trackName = vocalPair.Track.Title;
                }
                if (!string.IsNullOrWhiteSpace(trackName))
                    scheduleStateItem.MusicTrackName = trackName;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, AppConstants.Logging.ScheduleDisplayNameDiagnosticsLog.ErrorPopulatingMusicTrackName);
            }
        }
    }

    /// <summary>
    /// Fallback section display name for melody (e.g. "iam-1" -> "Volume 1") when DB lookup fails.
    /// </summary>
    private static string? GetMelodySectionDisplayNameFallback(string? publicationCode, string? sectionCode)
    {
        if (string.IsNullOrWhiteSpace(publicationCode) || string.IsNullOrWhiteSpace(sectionCode))
            return null;
        if (!string.Equals(publicationCode, AppConstants.Media.MelodyMusicPublicationCodeIam, StringComparison.OrdinalIgnoreCase))
            return null;
        var discPrefix = $"{AppConstants.Media.MelodyMusicPublicationCodeIam}-";
        if (sectionCode.Length > discPrefix.Length && sectionCode.StartsWith(discPrefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(sectionCode.AsSpan(discPrefix.Length), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var vol))
            return AppConstants.Media.PublicationUiMelodyVolumePrefix + vol.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return null;
    }
}
