#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Services.Bootstrap;

/// <summary>
/// Helper class for populating schedule state items with display names and metadata.
/// </summary>
internal sealed class ScheduleStatePopulator
{
    private readonly IMapper mapper;
    private readonly LookupDataCollector keyCollector;
    private readonly LookupDataLoader dataLoader;
    private readonly BiblePublicationDisplayNamePopulator biblePublicationPopulator;
    private readonly MusicDisplayNamePopulator musicPopulator;
    private readonly DefaultMusicPopulator defaultMusicPopulator;
    private readonly IMediaService? mediaService;

    public ScheduleStatePopulator(
        IBiblePublicationService? BiblePublicationService,
        IBiblePublicationSectionService? biblePublicationSectionService,
        IMapper mapper,
        IMediaService? mediaService,
        IMelodyMusicService? melodyMusicService,
        IVocalMusicService? vocalMusicService)
    {
        this.mapper = mapper;
        this.mediaService = mediaService;
        keyCollector = new LookupDataCollector();
        dataLoader = new LookupDataLoader(BiblePublicationService, biblePublicationSectionService, mediaService, vocalMusicService);
        biblePublicationPopulator = new BiblePublicationDisplayNamePopulator();
        musicPopulator = new MusicDisplayNamePopulator();
        defaultMusicPopulator = new DefaultMusicPopulator(melodyMusicService);
    }

    public async Task<ObservableHashSet<ScheduleStateItem>> PopulateAsync(
        List<AlarmSchedule> alarmSchedules,
        Dictionary<string, Language>? languagesDict)
    {
        // Collect all unique keys needed
        var keys = keyCollector.CollectKeys(alarmSchedules);

        // Batch load all required data upfront to avoid N+1 queries
        var lookupData = await dataLoader.LoadAllAsync(keys);

        // Process schedules in parallel instead of sequentially
        var scheduleTasks = alarmSchedules.Select(async schedule =>
        {
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(schedule);

            // Use pre-loaded lookup data instead of making individual queries
            biblePublicationPopulator.Populate(
                schedule,
                scheduleStateItem,
                lookupData,
                languagesDict);

            musicPopulator.Populate(
                schedule,
                scheduleStateItem,
                lookupData);

            return scheduleStateItem;
        });

        var scheduleStateItems = await Task.WhenAll(scheduleTasks);

        // Batch populate default music for all schedules that need it
        await defaultMusicPopulator.PopulateBatchAsync(
            alarmSchedules,
            scheduleStateItems);

        // Some publications (e.g., "iam") are stored without a language FK and cannot be loaded
        // via IBiblePublicationService language-bound queries during lookup loading.
        // Fill missing section/track display names using IMediaService (which handles no-language publications).
        await PopulateMissingBibleDisplayNamesAsync(alarmSchedules, scheduleStateItems);

        var initialSchedules = new ObservableHashSet<ScheduleStateItem>();
        foreach (var item in scheduleStateItems)
        {
            initialSchedules.Add(item);
        }
        return initialSchedules;
    }

    private async Task PopulateMissingBibleDisplayNamesAsync(List<AlarmSchedule> schedules, ScheduleStateItem[] stateItems)
    {
        if (mediaService == null || schedules.Count == 0 || stateItems.Length != schedules.Count)
        {
            return;
        }

        var tasks = schedules.Select((schedule, idx) => PopulateMissingForScheduleAsync(schedule, stateItems[idx])).ToList();
        await Task.WhenAll(tasks);
    }

    private async Task PopulateMissingForScheduleAsync(AlarmSchedule schedule, ScheduleStateItem scheduleStateItem)
    {
        var ms = mediaService;
        if (ms == null)
        {
            return;
        }

        var bible = schedule.BiblePublicationSchedule;
        if (bible == null || string.IsNullOrWhiteSpace(bible.PublicationCode))
        {
            return;
        }

        var languageCode = bible.LanguageCode ?? string.Empty;
        var publicationCode = bible.PublicationCode;
        var sectionCode = bible.SectionCode;
        var sectionIndex = Bible.Alarm.Shared.Helpers.SectionCodeHelper.GetSectionIndexOrZero(sectionCode);

        // Publication name + category for publications that can't be loaded via language-bound lookups (e.g., no-language publications like "iam")
        if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationName) ||
            string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationCategoryName))
        {
            try
            {
                var pubs = await ms.GetBiblePublications(languageCode, categoryName: null, downloadAll: false, progress: null);
                var pub = pubs.Values.FirstOrDefault(p =>
                    p != null && string.Equals(p.PublicationCode, publicationCode, StringComparison.OrdinalIgnoreCase));

                if (pub != null)
                {
                    if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationName) && !string.IsNullOrWhiteSpace(pub.Name))
                    {
                        scheduleStateItem.BiblePublicationName = pub.Name;
                    }

                    if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationCategoryName) && pub.Category != null)
                    {
                        scheduleStateItem.BiblePublicationCategoryId = pub.CategoryId;
                        scheduleStateItem.BiblePublicationCategoryName = pub.Category.CategoryName;
                    }
                }
            }
            catch
            {
                // Best-effort only.
            }
        }

        // Section name for sectioned publications (including no-language ones like "iam-1")
        if (sectionIndex > 0 && string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationSectionName))
        {
            try
            {
                var sections = await ms.GetBiblePublicationSections(languageCode, publicationCode);
                if (sections != null && sections.Count > 0)
                {
                    // Prefer exact SectionCode match; fall back to numeric key.
                    var match = sections.Values.FirstOrDefault(s =>
                        s != null &&
                        !string.IsNullOrWhiteSpace(s.SectionCode) &&
                        string.Equals(s.SectionCode, sectionCode, StringComparison.OrdinalIgnoreCase));

                    if (match != null && !string.IsNullOrWhiteSpace(match.Name))
                    {
                        scheduleStateItem.BiblePublicationSectionName = match.Name;
                    }
                    else if (sections.TryGetValue(sectionIndex, out var sectionByIndex) &&
                             sectionByIndex != null &&
                             !string.IsNullOrWhiteSpace(sectionByIndex.Name))
                    {
                        scheduleStateItem.BiblePublicationSectionName = sectionByIndex.Name;
                    }
                }
            }
            catch
            {
                // Best-effort only; bootstrap should not fail due to display name lookups.
            }
        }

        // Track title for sectioned publications (needed for Music/iam home subtitle)
        if (sectionIndex > 0 && bible.TrackNumber > 0 && string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationTrackTitle))
        {
            try
            {
                var tracks = await ms.GetBiblePublicationTracks(languageCode, publicationCode, sectionIndex);
                if (tracks != null && tracks.TryGetValue(bible.TrackNumber, out var track) && track != null)
                {
                    if (!string.IsNullOrWhiteSpace(track.Title))
                    {
                        scheduleStateItem.BiblePublicationTrackTitle = track.Title;
                    }
                }
            }
            catch
            {
                // Best-effort only.
            }
        }
    }
}
