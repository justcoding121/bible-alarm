#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Effects.Services.Helpers;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Effects.Services;

/// <summary>
/// Handles population of display names for schedule state items.
/// Separated from ScheduleEffects for better modularity.
/// </summary>
public sealed class ScheduleDisplayNamePopulator
{
    private readonly BiblePublicationNamePopulator biblePublicationNamePopulator;
    private readonly MusicNamePopulator musicNamePopulator;

    public ScheduleDisplayNamePopulator(
        IBiblePublicationService? BiblePublicationService = null,
        IBiblePublicationSectionService? biblePublicationSectionService = null,
        IMediaService? mediaService = null)
    {
        var biblePubService = BiblePublicationService ?? ServiceProviderManager.GetService<IBiblePublicationService>();
        var sectionService = biblePublicationSectionService ?? ServiceProviderManager.GetService<IBiblePublicationSectionService>();
        var mediaSvc = mediaService ?? ServiceProviderManager.GetService<IMediaService>();

        this.biblePublicationNamePopulator = new BiblePublicationNamePopulator(
            biblePubService, sectionService, mediaSvc);
        this.musicNamePopulator = new MusicNamePopulator(sectionService, mediaSvc);
    }

    /// <summary>
    /// Populate BiblePublicationLanguageName from language dictionary if BiblePublicationSchedule exists.
    /// </summary>
    public async Task PopulatePublicationNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        await biblePublicationNamePopulator.PopulatePublicationNameAsync(scheduleStateItem, schedule);
    }

    /// <summary>
    /// Populate BiblePublicationLanguageName from language dictionary using language code from ScheduleStateItem.
    /// </summary>
    public async Task PopulatePublicationNameAsync(ScheduleStateItem scheduleStateItem)
    {
        await biblePublicationNamePopulator.PopulatePublicationNameAsync(scheduleStateItem);
    }

    /// <summary>
    /// Populate BiblePublicationName from BiblePublicationService if BiblePublicationSchedule exists.
    /// </summary>
    public async Task PopulateBiblePublicationNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        await biblePublicationNamePopulator.PopulateBiblePublicationNameAsync(scheduleStateItem, schedule);
    }

    /// <summary>
    /// Populate SectionName from BiblePublicationSectionService if BiblePublicationSchedule exists.
    /// </summary>
    public async Task PopulateSectionNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        await biblePublicationNamePopulator.PopulateSectionNameAsync(scheduleStateItem, schedule);
    }

    /// <summary>
    /// Populate TrackTitle for all Bible publications from BiblePublicationService.
    /// For sectioned publications (traditional Bible), loads track from section.
    /// For non-sectioned publications (drama/video), loads track directly from publication.
    /// </summary>
    public async Task PopulateTrackTitleAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        await biblePublicationNamePopulator.PopulateTrackTitleAsync(scheduleStateItem, schedule);
    }

    /// <summary>
    /// Populate MusicLanguageName from vocal music languages if Music exists and is Vocals.
    /// </summary>
    public async Task PopulateMusicLanguageNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        await musicNamePopulator.PopulateMusicLanguageNameAsync(scheduleStateItem, schedule);
    }

    /// <summary>
    /// Populate MusicPublicationName from music releases (vocals or melodies).
    /// </summary>
    public async Task PopulateMusicPublicationNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        await musicNamePopulator.PopulateMusicPublicationNameAsync(scheduleStateItem, schedule);
    }

    /// <summary>
    /// Populate MusicSectionName from music sections if Music exists and has a section code.
    /// </summary>
    public async Task PopulateMusicSectionNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        await musicNamePopulator.PopulateMusicSectionNameAsync(scheduleStateItem, schedule);
    }

    /// <summary>
    /// Populate MusicTrackName from music tracks if Music exists.
    /// </summary>
    public async Task PopulateMusicTrackNameAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        await musicNamePopulator.PopulateMusicTrackNameAsync(scheduleStateItem, schedule);
    }
}

