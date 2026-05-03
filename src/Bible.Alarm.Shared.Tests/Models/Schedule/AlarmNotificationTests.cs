#nullable enable

using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmNotificationTests
{
    [Fact]
    public void Properties_assign_and_roundtrip_on_entity()
    {
        var scheduled = DateTimeOffset.Parse("2027-06-01T08:09:10Z");

        var schedule = new AlarmSchedule
        {
            Id = 7,
            Name = "Parent",
            IsEnabled = true,
            Hour = 8,
            Minute = 9,
            Second = 10,
            DaysOfWeek = WeekDays.Monday | WeekDays.Wednesday,
            NotificationEnabled = true,
            MusicEnabled = false,
        };

        var sut = new AlarmNotification
        {
            Id = 100,
            ScheduledTime = scheduled,
            Sent = false,
            Fired = false,
            AlarmScheduleId = schedule.Id,
            AlarmSchedule = schedule,
            CancellationRequested = true,
            Cancelled = false,
        };

        Assert.Equal(100L, sut.Id);
        Assert.Equal(scheduled, sut.ScheduledTime);
        Assert.False(sut.Sent);
        Assert.False(sut.Fired);
        Assert.Equal(7, sut.AlarmScheduleId);
        Assert.Same(schedule, sut.AlarmSchedule);
        Assert.True(sut.CancellationRequested);
        Assert.False(sut.Cancelled);

        sut.Sent = true;
        sut.Cancelled = true;
        Assert.True(sut.Sent);
        Assert.True(sut.Cancelled);
    }
}
