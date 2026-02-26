#nullable enable

using System.Linq;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Refreshes schedule display names (section, track) after navigating to a newly harvested section during playback.
/// Preserves the Bible schedule language code from DB/state; language code is only updated when the user clicks Save on the schedule page.
/// </summary>
public sealed class PlaylistScheduleDisplayRefresher
{
    private readonly IAlarmScheduleService? alarmScheduleService;
    private readonly IScheduleDisplayNameService? scheduleDisplayNameService;
    private readonly IState<ApplicationState> applicationState;
    private readonly IDispatcher dispatcher;
    private readonly ILogger logger;

    public PlaylistScheduleDisplayRefresher(
        IAlarmScheduleService? alarmScheduleService,
        IScheduleDisplayNameService? scheduleDisplayNameService,
        IState<ApplicationState> applicationState,
        IDispatcher dispatcher,
        ILogger logger)
    {
        this.alarmScheduleService = alarmScheduleService;
        this.scheduleDisplayNameService = scheduleDisplayNameService;
        this.applicationState = applicationState;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    /// <summary>
    /// Preserves the Bible schedule language code: use BiblePublicationSchedule.LanguageCode from DB when set.
    /// When DB has null (e.g. no-language publication iam), keep the value already in state so we do not
    /// overwrite it. Language code may only be updated when the user clicks Save on the schedule page.
    /// </summary>
    private string GetBiblePublicationLanguageCodeToPreserve(int scheduleId, string? dbLanguageCode)
    {
        if (!string.IsNullOrWhiteSpace(dbLanguageCode))
            return dbLanguageCode;
        var state = applicationState.Value;
        if (state.CurrentSchedule?.Id == scheduleId && !string.IsNullOrWhiteSpace(state.CurrentSchedule.BiblePublicationLanguageCode))
            return state.CurrentSchedule.BiblePublicationLanguageCode;
        var fromList = state.Schedules?.FirstOrDefault(s => s.Id == scheduleId);
        return !string.IsNullOrWhiteSpace(fromList?.BiblePublicationLanguageCode)
            ? fromList.BiblePublicationLanguageCode
            : AppConstants.Media.DefaultLanguageCode;
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

            var languageCodeToPreserve = GetBiblePublicationLanguageCodeToPreserve(schedule.Id, schedule.BiblePublicationSchedule.LanguageCode);

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
                    LanguageCode = languageCodeToPreserve,
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
