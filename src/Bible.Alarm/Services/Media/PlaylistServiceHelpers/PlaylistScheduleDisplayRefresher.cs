#nullable enable

using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Refreshes schedule display names after navigating to a newly harvested section.
/// </summary>
public sealed class PlaylistScheduleDisplayRefresher
{
    private readonly IAlarmScheduleService? alarmScheduleService;
    private readonly IScheduleDisplayNameService? scheduleDisplayNameService;
    private readonly IDispatcher dispatcher;
    private readonly ILogger logger;

    public PlaylistScheduleDisplayRefresher(
        IAlarmScheduleService? alarmScheduleService,
        IScheduleDisplayNameService? scheduleDisplayNameService,
        IDispatcher dispatcher,
        ILogger logger)
    {
        this.alarmScheduleService = alarmScheduleService;
        this.scheduleDisplayNameService = scheduleDisplayNameService;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    public async Task RefreshAsync(int scheduleId, string languageCode, string publicationCode, string? sectionCode, CancellationToken cancellationToken)
    {
        if (scheduleDisplayNameService == null || alarmScheduleService == null)
        {
            return;
        }

        try
        {
            var schedule = await alarmScheduleService.GetScheduleByIdAsync(
                scheduleId,
                includeMusic: false,
                includeBiblePublication: true,
                cancellationToken);

            if (schedule?.BiblePublicationSchedule == null)
            {
                return;
            }

            var tempSchedule = new AlarmSchedule
            {
                Id = schedule.Id,
                Name = schedule.Name,
                IsEnabled = schedule.IsEnabled,
                Hour = schedule.Hour,
                Minute = schedule.Minute,
                Second = schedule.Second,
                DaysOfWeek = schedule.DaysOfWeek,
                NotificationEnabled = schedule.NotificationEnabled,
                MusicEnabled = schedule.MusicEnabled,
                SnoozeMinutes = schedule.SnoozeMinutes,
                NumberOfTracksToPlay = schedule.NumberOfTracksToPlay,
                AlwaysPlayFromStart = schedule.AlwaysPlayFromStart,
                BiblePublicationSchedule = new BiblePublicationSchedule
                {
                    LanguageCode = schedule.BiblePublicationSchedule.LanguageCode ?? languageCode,
                    PublicationCode = schedule.BiblePublicationSchedule.PublicationCode ?? publicationCode,
                    SectionCode = sectionCode,
                    TrackCode = schedule.BiblePublicationSchedule.TrackCode
                }
            };

            var scheduleStateItem = new ScheduleStateItem
            {
                Id = schedule.Id,
                BiblePublicationLanguageCode = tempSchedule.BiblePublicationSchedule.LanguageCode,
                BiblePublicationCode = tempSchedule.BiblePublicationSchedule.PublicationCode,
                BiblePublicationSectionCode = tempSchedule.BiblePublicationSchedule.SectionCode,
                BiblePublicationTrackCode = tempSchedule.BiblePublicationSchedule.TrackCode
            };

            await scheduleDisplayNameService.PopulateDisplayNamesAsync(scheduleStateItem, tempSchedule);

            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(scheduleStateItem, false, false, shouldSave: false));

            logger.Debug("Refreshed schedule display names after section harvest: ScheduleId={ScheduleId}, SectionCode={SectionCode}, SectionName={SectionName}",
                scheduleId, sectionCode, scheduleStateItem.BiblePublicationSectionName);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to refresh schedule display names after section harvest: ScheduleId={ScheduleId}, SectionCode={SectionCode}",
                scheduleId, sectionCode);
        }
    }
}
