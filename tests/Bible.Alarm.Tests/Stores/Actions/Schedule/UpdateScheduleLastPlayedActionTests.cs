#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;

namespace Bible.Alarm.Tests;

public sealed class UpdateScheduleLastPlayedActionTests
{
    [Fact]
    public void Constructor_sets_schedule_and_timestamp()
    {
        var when = new DateTime(2026, 5, 12, 14, 30, 0, DateTimeKind.Utc);

        var sut = new UpdateScheduleLastPlayedAction(33, when);

        Assert.Equal(33, sut.ScheduleId);
        Assert.Equal(when, sut.LastPlayedAtUtc);
    }
}
