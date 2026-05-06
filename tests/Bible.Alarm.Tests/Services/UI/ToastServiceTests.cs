#nullable enable

using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Services.UI;

namespace Bible.Alarm.Tests;

public sealed class ToastServiceTests
{
    private sealed class RecordingToastService : ToastService
    {
        public List<(string Message, int Seconds)> Calls { get; } = [];

        public override Task ShowMessage(string message, int seconds = 3)
        {
            Calls.Add((message, seconds));
            return Task.CompletedTask;
        }

        public override Task Clear() => Task.CompletedTask;
    }

    private static AlarmSchedule DailyScheduleAt(int hour, int minute)
    {
        var s = new AlarmSchedule
        {
            Id = 1,
            Name = "Daily",
            IsEnabled = true,
            Hour = hour,
            Minute = minute,
            Second = 0,
            DaysOfWeek = WeekDays.All,
            NotificationEnabled = true,
            MusicEnabled = false,
        };
        return s;
    }

    private static readonly WeekDays[] WeeklySingleDays =
    [
        WeekDays.Sunday,
        WeekDays.Monday,
        WeekDays.Tuesday,
        WeekDays.Wednesday,
        WeekDays.Thursday,
        WeekDays.Friday,
        WeekDays.Saturday
    ];

    private static AlarmSchedule FirstWeeklyScheduleWhereNextFireIsMoreThanOneDayAway()
    {
        foreach (var day in WeeklySingleDays)
        {
            var s = new AlarmSchedule
            {
                Id = 2,
                Name = "Weekly",
                IsEnabled = true,
                Hour = 12,
                Minute = 0,
                Second = 0,
                DaysOfWeek = day,
                NotificationEnabled = true,
                MusicEnabled = false,
            };

            if ((s.NextFireDate() - DateTimeOffset.Now).Days > 0)
            {
                return s;
            }
        }

        throw new InvalidOperationException("Expected at least one weekday-only schedule to fire more than one calendar day away.");
    }

    [Fact]
    public async Task ShowScheduledNotification_formats_message_with_next_fire_gap()
    {
        var sut = new RecordingToastService();
        var schedule = DailyScheduleAt(hour: 7, minute: 15);

        await sut.ShowScheduledNotification(schedule);

        var call = Assert.Single(sut.Calls);
        Assert.Equal(3, call.Seconds);
        Assert.StartsWith("Reminder set for", call.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShowScheduledNotification_includes_day_count_when_next_fire_is_over_one_day_away()
    {
        var sut = new RecordingToastService();
        var schedule = FirstWeeklyScheduleWhereNextFireIsMoreThanOneDayAway();

        await sut.ShowScheduledNotification(schedule, seconds: 4);

        var call = Assert.Single(sut.Calls);
        Assert.Equal(4, call.Seconds);
        Assert.Contains(" days, ", call.Message, StringComparison.Ordinal);
        Assert.Contains(" hours and ", call.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var sut = new RecordingToastService();

        sut.Dispose();
        sut.Dispose();
    }
}
