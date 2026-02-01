#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Microsoft.Extensions.DependencyInjection;

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
        IVocalMusicService? vocalMusicService,
        IServiceScopeFactory? scopeFactory)
    {
        this.mapper = mapper;
        this.mediaService = mediaService;
        keyCollector = new LookupDataCollector();
        dataLoader = new LookupDataLoader(BiblePublicationService, biblePublicationSectionService, mediaService, vocalMusicService, scopeFactory);
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

        var initialSchedules = new ObservableHashSet<ScheduleStateItem>();
        foreach (var item in scheduleStateItems)
        {
            initialSchedules.Add(item);
        }
        return initialSchedules;
    }
}
