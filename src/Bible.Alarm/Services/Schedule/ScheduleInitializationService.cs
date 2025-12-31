#nullable enable
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Services.Schedule;

public sealed class ScheduleInitializationService : IScheduleInitializationService
{
    private readonly ILogger logger;
    private readonly IBibleTranslationService? bibleTranslationService;
    private readonly IMelodyMusicService melodyMusicService;
    private readonly IMapper mapper;
    private readonly IScheduleDisplayNameService scheduleDisplayNameService;

    public ScheduleInitializationService(
        ILogger logger,
        IBibleTranslationService? bibleTranslationService,
        IMelodyMusicService melodyMusicService,
        IMapper mapper,
        IScheduleDisplayNameService scheduleDisplayNameService)
    {
        this.logger = logger;
        this.bibleTranslationService = bibleTranslationService;
        this.melodyMusicService = melodyMusicService;
        this.mapper = mapper;
        this.scheduleDisplayNameService = scheduleDisplayNameService;
    }

    public async Task<ScheduleStateItem> InitializeNewScheduleAsync()
    {
        var sampleSchedule = await AlarmSchedule.GetSampleSchedule(true, bibleTranslationService, melodyMusicService);

        // Map sample schedule to state item
        var scheduleStateItem = mapper.Map<ScheduleStateItem>(sampleSchedule);

        // Log the music type to verify it's Melodies (not Vocals)
        logger.Information("InitializeNewScheduleAsync: Mapped sample schedule. MusicType={MusicType}, MusicTrackNumber={TrackNumber}, MusicPublicationCode={PublicationCode}",
            scheduleStateItem.MusicType?.ToString() ?? "null",
            scheduleStateItem.MusicTrackNumber?.ToString() ?? "null",
            scheduleStateItem.MusicPublicationCode ?? "null");

        // Populate display names before dispatching action
        logger.Debug("InitializeNewScheduleAsync: Populating display names for new schedule");
        await scheduleDisplayNameService.PopulateDisplayNamesAsync(scheduleStateItem, sampleSchedule);
        logger.Debug("InitializeNewScheduleAsync: Display names populated. LanguageName: {LanguageName}, PublicationName: {PublicationName}, BookName: {BookName}",
            scheduleStateItem.BibleReadingLanguageName ?? "null",
            scheduleStateItem.BibleReadingPublicationName ?? "null",
            scheduleStateItem.BibleReadingBookName ?? "null");

        return scheduleStateItem;
    }

    public async Task CompleteScheduleLoadAsync()
    {
        // Wait briefly for initial state to settle
        await Task.Delay(50);
    }

    public void InitializeTrackingFields(
        ScheduleStateItem scheduleStateItem,
        ref int lastScheduleId,
        ref MusicType? lastMusicType,
        ref int? lastMusicTrackNumber,
        ref string? lastMusicPublicationCode,
        ref string? lastMusicLanguageCode,
        ref bool? lastMusicRepeat)
    {
        lastScheduleId = scheduleStateItem.Id;
        lastMusicType = scheduleStateItem.MusicType;
        lastMusicTrackNumber = scheduleStateItem.MusicTrackNumber;
        lastMusicPublicationCode = scheduleStateItem.MusicPublicationCode;
        lastMusicLanguageCode = scheduleStateItem.MusicLanguageCode;
        lastMusicRepeat = scheduleStateItem.MusicRepeat;
    }
}

