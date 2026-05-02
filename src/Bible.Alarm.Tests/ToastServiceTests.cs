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
    public void Dispose_is_idempotent()
    {
        var sut = new RecordingToastService();

        sut.Dispose();
        sut.Dispose();
    }
}
