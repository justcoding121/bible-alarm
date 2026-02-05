#nullable enable
using AutoMapper;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Services.Schedule;

public sealed class ScheduleInitializationService : IScheduleInitializationService
{
    private readonly ILogger logger;
    private readonly IBiblePublicationService? BiblePublicationService;
    private readonly IMelodyMusicService melodyMusicService;
    private readonly IMapper mapper;
    private readonly IScheduleDisplayNameService scheduleDisplayNameService;
    private readonly IAlarmScheduleService? alarmScheduleService;

    public ScheduleInitializationService(
        ILogger logger,
        IBiblePublicationService? BiblePublicationService,
        IMelodyMusicService melodyMusicService,
        IMapper mapper,
        IScheduleDisplayNameService scheduleDisplayNameService,
        IAlarmScheduleService? alarmScheduleService = null)
    {
        this.logger = logger;
        this.BiblePublicationService = BiblePublicationService;
        this.melodyMusicService = melodyMusicService;
        this.mapper = mapper;
        this.scheduleDisplayNameService = scheduleDisplayNameService;
        this.alarmScheduleService = alarmScheduleService;
    }

    public async Task<ScheduleStateItem> InitializeNewScheduleAsync()
    {
        if (BiblePublicationService == null || melodyMusicService == null)
        {
            throw new InvalidOperationException("Required services are not initialized");
        }
        var sampleSchedule = await AlarmSchedule.GetSampleSchedule(true, BiblePublicationService, melodyMusicService);

        // Map sample schedule to state item
        var scheduleStateItem = mapper.Map<ScheduleStateItem>(sampleSchedule);

        // Log the music settings (type is inferred from LanguageCode: NULL/empty = melody, otherwise = vocal)
        logger.Information("InitializeNewScheduleAsync: Mapped sample schedule. MusicLanguageCode={LanguageCode}, MusicTrackNumber={TrackNumber}, MusicPublicationCode={PublicationCode}, MusicSectionCode={SectionCode}",
            scheduleStateItem.MusicLanguageCode ?? "null",
            scheduleStateItem.MusicTrackNumber?.ToString() ?? "null",
            scheduleStateItem.MusicPublicationCode ?? "null",
            scheduleStateItem.MusicSectionCode ?? "null");

        // Populate display names before dispatching action
        logger.Debug("InitializeNewScheduleAsync: Populating display names for new schedule");
        await scheduleDisplayNameService.PopulateDisplayNamesAsync(scheduleStateItem, sampleSchedule);
        logger.Debug("InitializeNewScheduleAsync: Display names populated. LanguageName: {LanguageName}, PublicationName: {PublicationName}, SectionName: {SectionName}, MusicPublicationName: {MusicPublicationName}, MusicSectionName: {MusicSectionName}",
            scheduleStateItem.BiblePublicationLanguageName ?? "null",
            scheduleStateItem.BiblePublicationName ?? "null",
            scheduleStateItem.BiblePublicationSectionName ?? "null",
            scheduleStateItem.MusicPublicationName ?? "null",
            scheduleStateItem.MusicSectionName ?? "null");

        return scheduleStateItem;
    }

    public async Task<ScheduleStateItem?> LoadExistingScheduleAsync(int scheduleId, bool isEnabled)
    {
        if (alarmScheduleService == null)
        {
            logger.Warning("LoadExistingScheduleAsync: AlarmScheduleService not available");
            return null;
        }

        try
        {
            logger.Debug("LoadExistingScheduleAsync: Loading schedule {ScheduleId} from database", scheduleId);

            // Load schedule from database with all includes
            var schedule = await alarmScheduleService.GetScheduleByIdAsync(
                scheduleId,
                includeMusic: true,
                includeBiblePublication: true,
                CancellationToken.None);

            if (schedule == null)
            {
                logger.Warning("LoadExistingScheduleAsync: Schedule {ScheduleId} not found in database", scheduleId);
                return null;
            }

            // Map to ScheduleStateItem
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(schedule);
            scheduleStateItem.IsEnabled = isEnabled;

            // Populate display names
            await scheduleDisplayNameService.PopulateDisplayNamesAsync(scheduleStateItem, schedule);

            logger.Debug("LoadExistingScheduleAsync: Loaded schedule {ScheduleId} with display names", scheduleId);
            return scheduleStateItem;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "LoadExistingScheduleAsync: Error loading schedule {ScheduleId}", scheduleId);
            return null;
        }
    }

    public async Task CompleteScheduleLoadAsync()
    {
        // Wait briefly for initial state to settle
        await Task.Delay(50);
    }

    public void InitializeTrackingFields(
        ScheduleStateItem scheduleStateItem,
        ref int lastScheduleId,
        ref int? lastMusicTrackNumber,
        ref string? lastMusicPublicationCode,
        ref string? lastMusicLanguageCode,
        ref bool? lastMusicRepeat)
    {
        lastScheduleId = scheduleStateItem.Id;
        lastMusicTrackNumber = scheduleStateItem.MusicTrackNumber;
        lastMusicPublicationCode = scheduleStateItem.MusicPublicationCode;
        lastMusicLanguageCode = scheduleStateItem.MusicLanguageCode;
        lastMusicRepeat = scheduleStateItem.MusicRepeat;
    }
}

