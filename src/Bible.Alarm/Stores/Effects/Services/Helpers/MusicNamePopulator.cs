#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Stores.Effects.Services.Helpers;

/// <summary>
/// Helper class for populating music related display names.
/// </summary>
internal sealed class MusicNamePopulator
{
    private readonly IBiblePublicationSectionService? biblePublicationSectionService;
    private readonly IMediaService? mediaService;
    private readonly IVocalMusicService? vocalMusicService;

    public MusicNamePopulator(
        IBiblePublicationSectionService? biblePublicationSectionService,
        IMediaService? mediaService,
        IVocalMusicService? vocalMusicService)
    {
        this.biblePublicationSectionService = biblePublicationSectionService;
        this.mediaService = mediaService;
        this.vocalMusicService = vocalMusicService;
    }

    /// <summary>
    /// Populate MusicLanguageName from vocal music languages if Music exists and is Vocals.
    /// </summary>
    public async Task PopulateMusicLanguageNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.Music == null || mediaService == null)
        {
            return;
        }

        try
        {
            var music = schedule.Music;
            // Only populate for vocals (melodies don't have language)
            if (music.MusicType != MusicType.VocalMusic ||
                string.IsNullOrWhiteSpace(music.LanguageCode))
            {
                return;
            }

            var languagesDict = await mediaService.GetVocalMusicLanguages();
            if (languagesDict.TryGetValue(music.LanguageCode, out var language))
            {
                scheduleStateItem.MusicLanguageName = language.Name;
                scheduleStateItem.MusicLanguageDirection = language.Direction;
                Log.Debug("ScheduleEffects: Set MusicLanguageName '{MusicLanguageName}' and MusicLanguageDirection '{MusicLanguageDirection}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    language.Name, language.Direction, schedule.Id, music.LanguageCode);
            }
            else
            {
                scheduleStateItem.MusicLanguageName = music.LanguageCode;
                scheduleStateItem.MusicLanguageDirection = "ltr"; // Default to LTR if language not found
                Log.Debug("ScheduleEffects: Language not found for LanguageCode '{LanguageCode}', using code as MusicLanguageName for schedule {ScheduleId}",
                    music.LanguageCode, schedule.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating MusicLanguageName for schedule {ScheduleId}", schedule.Id);
            if (schedule.Music != null)
            {
                scheduleStateItem.MusicLanguageName = schedule.Music.LanguageCode;
                scheduleStateItem.MusicLanguageDirection = "ltr"; // Default to LTR on error
            }
        }
    }

    /// <summary>
    /// Populate MusicPublicationName from music releases (vocals or melodies).
    /// </summary>
    public async Task PopulateMusicPublicationNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.Music == null || mediaService == null)
        {
            return;
        }

        try
        {
            var music = schedule.Music;
            if (string.IsNullOrWhiteSpace(music.PublicationCode))
            {
                return;
            }

            if (music.MusicType == MusicType.VocalMusic)
            {
                // Populate for vocals
                if (string.IsNullOrWhiteSpace(music.LanguageCode))
                {
                    return;
                }

                // Avoid loading *all* vocal music releases just to get one name.
                var release = vocalMusicService != null
                    ? await vocalMusicService.GetByLanguageAndCodeAsync(music.LanguageCode, music.PublicationCode)
                    : null;
                if (release != null)
                {
                    scheduleStateItem.MusicPublicationName = release.Name;
                    Log.Debug("ScheduleEffects: Set MusicPublicationName '{MusicPublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                        release.Name, schedule.Id, music.PublicationCode);
                }
            }
            else if (music.MusicType == MusicType.Music)
            {
                // Populate for melodies
                var releases = await mediaService.GetMelodyMusicReleases();
                if (releases.TryGetValue(music.PublicationCode, out var melodyRelease))
                {
                    scheduleStateItem.MusicPublicationName = melodyRelease.Name;
                    Log.Debug("ScheduleEffects: Set MusicPublicationName '{MusicPublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                        melodyRelease.Name, schedule.Id, music.PublicationCode);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating MusicPublicationName for schedule {ScheduleId}", schedule.Id);
        }
    }

    /// <summary>
    /// Populate MusicSectionName from music sections if Music exists and has a section code.
    /// Also handles the case where we have a track number but no section code - finds the section containing the track.
    /// </summary>
    public async Task PopulateMusicSectionNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.Music == null)
        {
            return;
        }

        try
        {
            var music = schedule.Music;
            if (string.IsNullOrWhiteSpace(music.PublicationCode))
            {
                return;
            }

            // If we have a section code, use it to populate the section name
            if (!string.IsNullOrWhiteSpace(music.SectionCode))
            {
                // Convert SectionCode to int for lookup
                var sectionNumber = await SectionCodeConverter.ConvertToIntAsync(
                    music.SectionCode,
                    music.LanguageCode ?? string.Empty, // For melodies, LanguageCode is null
                    music.PublicationCode);

                if (sectionNumber <= 0)
                {
                    return;
                }

                // For vocal music, we need language code
                if (music.MusicType == MusicType.VocalMusic)
                {
                    if (string.IsNullOrWhiteSpace(music.LanguageCode) || biblePublicationSectionService == null)
                    {
                        return;
                    }

                    var sectionName = await biblePublicationSectionService.GetSectionNameAsync(
                        music.LanguageCode,
                        music.PublicationCode,
                        sectionNumber);

                    if (!string.IsNullOrWhiteSpace(sectionName))
                    {
                        scheduleStateItem.MusicSectionName = sectionName;
                        Log.Debug("ScheduleEffects: Set MusicSectionName '{MusicSectionName}' for schedule {ScheduleId} (SectionCode: {SectionCode})",
                            sectionName, schedule.Id, music.SectionCode);
                    }
                }
                else if (music.MusicType == MusicType.Music)
                {
                    // For melodies, music publications are BiblePublications with Category=Music and LanguageId=null
                    // We need to query sections directly from the database since LanguageId is null
                    // Query BiblePublicationSections directly by publication code and section number
                    try
                    {
                        var serviceProvider = MauiAppHolder.Services;
                        if (serviceProvider == null)
                        {
                            Log.Warning("ScheduleEffects: ServiceProvider not available for music section lookup in schedule {ScheduleId}", schedule.Id);
                            return;
                        }

                        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
                        using var scope = scopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

                        var section = await dbContext.BiblePublicationSections
                            .AsNoTracking()
                            .Include(x => x.BiblePublication)
                                .ThenInclude(x => x.Category)
                            .Where(x => x.BiblePublication.PublicationCode == music.PublicationCode
                                && x.BiblePublication.Category.CategoryName == "Music"
                                && x.BiblePublication.LanguageId == null
                                && x.SectionCode != null && x.SectionCode.Equals(music.SectionCode, StringComparison.OrdinalIgnoreCase))
                            .Select(x => x.Name)
                            .FirstOrDefaultAsync();

                        if (!string.IsNullOrWhiteSpace(section))
                        {
                            scheduleStateItem.MusicSectionName = section;
                            Log.Debug("ScheduleEffects: Set MusicSectionName '{MusicSectionName}' for schedule {ScheduleId} (SectionCode: {SectionCode})",
                                section, schedule.Id, music.SectionCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "ScheduleEffects: Error querying music section for melody in schedule {ScheduleId}", schedule.Id);
                    }
                }
            }
            // If we don't have a section code but we have a track number, find which section contains the track
            // Only if the publication has section structure (following Bible container pattern)
            else if (music.TrackNumber > 0 && music.MusicType == MusicType.Music)
            {
                // Check if publication has section structure (following Bible container pattern)
                // Only populate section name if publication actually has sections
                if (!Bible.Alarm.Shared.Helpers.PublicationTypeHelper.HasSectionStructure(music.PublicationCode))
                {
                    // Non-sectioned publication - clear section code/name if they exist
                    if (!string.IsNullOrWhiteSpace(scheduleStateItem.MusicSectionCode))
                    {
                        scheduleStateItem.MusicSectionCode = null;
                        scheduleStateItem.MusicSectionName = null;
                        if (schedule.Music != null)
                        {
                            schedule.Music.SectionCode = null;
                        }
                        Log.Debug("ScheduleEffects: Cleared MusicSectionCode and MusicSectionName for non-sectioned publication {PublicationCode} in schedule {ScheduleId}",
                            music.PublicationCode, schedule.Id);
                    }
                    return;
                }

                // For instrumental music with section structure, find the section that contains this track number
                try
                {
                    var serviceProvider = MauiAppHolder.Services;
                    if (serviceProvider == null)
                    {
                        Log.Warning("ScheduleEffects: ServiceProvider not available for music section lookup by track in schedule {ScheduleId}", schedule.Id);
                        return;
                    }

                    var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
                    using var scope = scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

                    // Find the section that contains this track number
                    var sectionInfo = await dbContext.BiblePublicationTracks
                        .AsNoTracking()
                        .Include(t => t.Section)
                            .ThenInclude(s => s.BiblePublication)
                                .ThenInclude(p => p.Category)
                        .Where(t => t.BiblePublicationSectionId != null
                            && t.Number == music.TrackNumber
                            && t.Publication.PublicationCode == music.PublicationCode
                            && t.Publication.Category.CategoryName == "Music"
                            && t.Publication.LanguageId == null)
                        .Select(t => new { t.Section!.SectionCode, t.Section.Name })
                        .FirstOrDefaultAsync();

                    if (sectionInfo != null && !string.IsNullOrWhiteSpace(sectionInfo.SectionCode))
                    {
                        // Update both the section code and name in the schedule entity and state item
                        if (schedule.Music != null)
                        {
                            schedule.Music.SectionCode = sectionInfo.SectionCode;
                        }
                        scheduleStateItem.MusicSectionName = sectionInfo.Name;
                        scheduleStateItem.MusicSectionCode = sectionInfo.SectionCode;
                        
                        Log.Debug("ScheduleEffects: Found and set MusicSectionCode '{MusicSectionCode}' and MusicSectionName '{MusicSectionName}' for schedule {ScheduleId} from track {TrackNumber}",
                            sectionInfo.SectionCode, sectionInfo.Name, schedule.Id, music.TrackNumber);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "ScheduleEffects: Error finding music section by track number for schedule {ScheduleId}", schedule.Id);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating MusicSectionName for schedule {ScheduleId}", schedule.Id);
        }
    }

    /// <summary>
    /// Populate MusicTrackName from music tracks if Music exists.
    /// </summary>
    public async Task PopulateMusicTrackNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.Music == null || mediaService == null)
        {
            return;
        }

        try
        {
            var music = schedule.Music;
            if (music.TrackNumber <= 0)
            {
                return;
            }

            string? trackName = null;
            if (music.MusicType == MusicType.Music)
            {
                if (string.IsNullOrWhiteSpace(music.PublicationCode))
                {
                    return;
                }

                // IMPORTANT: Sectioned melody publications (e.g., "iam") have duplicate track numbers across discs.
                // Always resolve track title from the selected disc (SectionCode) when sectioned.
                SortedDictionary<int, Bible.Alarm.Shared.Models.Media.Music.MusicTrack> tracks;
                if (PublicationTypeHelper.HasSectionStructure(music.PublicationCode))
                {
                    if (string.IsNullOrWhiteSpace(music.SectionCode))
                    {
                        return;
                    }
                    tracks = await mediaService.GetMelodyMusicTracksBySection(music.PublicationCode, music.SectionCode);
                }
                else
                {
                    tracks = await mediaService.GetMelodyMusicTracks(music.PublicationCode);
                }
                if (tracks.TryGetValue(music.TrackNumber, out var track))
                {
                    // Format melody track title with prefix to match track modal display
                    trackName = $"Melody Number(s) {track.Title}";
                }
            }
            else if (music.MusicType == MusicType.VocalMusic)
            {
                if (string.IsNullOrWhiteSpace(music.LanguageCode) || string.IsNullOrWhiteSpace(music.PublicationCode))
                {
                    return;
                }

                var tracks = await mediaService.GetVocalMusicTracks(music.LanguageCode, music.PublicationCode);
                if (tracks.TryGetValue(music.TrackNumber, out var track))
                {
                    trackName = track.Title;
                }
            }

            if (!string.IsNullOrWhiteSpace(trackName))
            {
                scheduleStateItem.MusicTrackName = trackName;
                Log.Debug("ScheduleEffects: Set MusicTrackName '{MusicTrackName}' for schedule {ScheduleId} (TrackNumber: {TrackNumber})",
                    trackName, schedule.Id, music.TrackNumber);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ScheduleEffects: Error populating MusicTrackName for schedule {ScheduleId}", schedule.Id);
        }
    }
}
