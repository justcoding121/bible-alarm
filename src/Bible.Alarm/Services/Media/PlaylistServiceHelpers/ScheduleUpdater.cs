#nullable enable
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media.Bible;
using Serilog;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Handles schedule updates for chapter navigation.
/// </summary>
public sealed class ScheduleUpdater(
    ILogger logger,
    IAlarmScheduleService alarmScheduleService,
    CancellationToken cancellationToken)
{
    /// <summary>
    /// Updates schedule to the next chapter.
    /// </summary>
    public async Task<AlarmSchedule> UpdateScheduleToNextChapterAsync(int scheduleId, KeyValuePair<BibleBook, BibleChapter> next)
    {
        return await alarmScheduleService.UpdateScheduleByIdAsync(
            scheduleId,
            s =>
            {
                var brs = s.BibleReadingSchedule ?? throw new ArgumentException($"BibleReadingSchedule is null for schedule {scheduleId}");
                brs.BookNumber = next.Key.Number;
                brs.ChapterNumber = next.Value.Number;
                brs.FinishedDuration = TimeSpan.Zero;
            },
            cancellationToken);
    }

    /// <summary>
    /// Updates schedule to the previous chapter.
    /// </summary>
    public async Task<AlarmSchedule> UpdateScheduleToPreviousChapterAsync(int scheduleId, KeyValuePair<BibleBook, BibleChapter> previous)
    {
        return await alarmScheduleService.UpdateScheduleByIdAsync(
            scheduleId,
            s =>
            {
                var brs = s.BibleReadingSchedule ?? throw new ArgumentException($"BibleReadingSchedule is null for schedule {scheduleId}");
                brs.BookNumber = previous.Key.Number;
                brs.ChapterNumber = previous.Value.Number;
                brs.FinishedDuration = TimeSpan.Zero;
            },
            cancellationToken);
    }

    /// <summary>
    /// Gets schedule with Bible reading validation.
    /// </summary>
    public async Task<AlarmSchedule> GetScheduleWithBibleReadingAsync(int scheduleId)
    {
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, false, true, cancellationToken);

        if (schedule?.BibleReadingSchedule == null)
        {
            throw new ArgumentException($"BibleReadingSchedule is null for schedule {scheduleId}");
        }

        return schedule;
    }
}
