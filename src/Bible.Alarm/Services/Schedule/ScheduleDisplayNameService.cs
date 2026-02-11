#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.Schedule.ScheduleDisplayNameServiceHelpers;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Services.Schedule;

public sealed class ScheduleDisplayNameService : IScheduleDisplayNameService
{
    private readonly ScheduleDisplayNameBibleHelper bibleHelper;
    private readonly ScheduleDisplayNameMusicHelper musicHelper;

    public ScheduleDisplayNameService(
        ILogger logger,
        IBiblePublicationService? biblePublicationService,
        IMediaService mediaService,
        IServiceProvider serviceProvider)
    {
        bibleHelper = new ScheduleDisplayNameBibleHelper(logger, biblePublicationService, mediaService, serviceProvider);
        musicHelper = new ScheduleDisplayNameMusicHelper(logger, mediaService, serviceProvider);
    }

    public async Task PopulateDisplayNamesAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule)
    {
        if (schedule.BiblePublicationSchedule != null)
            await bibleHelper.PopulateAsync(scheduleStateItem, schedule.BiblePublicationSchedule);
        if (schedule.Music != null)
            await musicHelper.PopulateAsync(scheduleStateItem, schedule.Music);
    }
}
